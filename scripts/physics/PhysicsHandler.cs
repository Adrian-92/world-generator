using Godot;
using System;
using System.Collections.Generic;
using static GlobalConstants;

public partial class PhysicsHandler : Node
{
	[Export] public WorldGenerator Generator;
	[Export] public TileMapLayer TargetLayer;
	[Export] public TileMapLayer Background;
	[Export] public double TickRate = 0.5;
	[Export] public double CleanupRate = 5;
	[Export] public double OvergrowRate = 10;
	[Export] public int SimulationRadius = 5;
	[Export] public int UnloadRadius = 10;
	[Export] public int VegetationLimit = 8;
	private System.Random _rng = new System.Random();
	private double _timer = 0;
	private double _cleanupTimer = 0;
	private double _overgrowTimer = 0;
	private int airTileID = 0;
	private int earthTileID = 1;
	private int grassTileID = 2;
	private int bedrockTileID = 12;
	private int maxOvergrowDepth = 30;
	private const int maxTilesPerTick = 1000; // limits simulation for performance
	private List<Vector2I> _activeBuffer = new List<Vector2I>();
	private List<Vector2I> _vegetationBuffer = new List<Vector2I>();
	private Dictionary<Vector2I, Chunk> _chunks = new Dictionary<Vector2I, Chunk>();
	private Dictionary<Vector2I, int[,]> _chunkCache = new Dictionary<Vector2I, int[,]>();

	private Dictionary<int, TileProperty> _tileData = new Dictionary<int, TileProperty>() {
		
		{0, new TileProperty {Type = TileType.AIR} },
		{1, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(1, 0), Type = TileType.STATIC} }, // Erde
		{2, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(0, 0), Type = TileType.STATIC} }, // Gras
		{3, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(15, 0), Type = TileType.LIQUID, Viscosity = 1.0f} }, // Wasser
		{4, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(6, 0), Type = TileType.LIQUID, Viscosity = 0.2f} }, // Magma
		{5, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(2, 0), Type = TileType.STATIC} },  // Dunkle Erde
		{6, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(3, 0), Type = TileType.STATIC} },  // Stein
		{7, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(4, 0), Type = TileType.STATIC} },  // Dunkler Stein
		{8, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(7, 0), Type = TileType.STATIC} }, // Kohle
		{9, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(8, 0), Type = TileType.STATIC} }, // Eisen
		{10, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(9, 0), Type = TileType.STATIC} }, // Gold
		{11, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(10, 0), Type = TileType.STATIC} }, // Diamant
		{12, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(12, 0), Type = TileType.STATIC} }, // Bedrock
		{13, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(12, 0), Type = TileType.STATIC} },
		{14, new TileProperty {AtlasID = 1, AtlasCoords = new Vector2I(13, 0), Type = TileType.STATIC} },
	};
	private Dictionary<int, TileProperty> _backgroundTileData = new Dictionary<int, TileProperty>() {
		{0, new TileProperty {Type = TileType.AIR} },
		{1, new TileProperty {AtlasID = 0, AtlasCoords = new Vector2I(2, 2)} }, // Erde

	};
	
	/* ################## CHUNK LOGIC ################## */
	
	public void UpdateChunksAround(Vector2 worldPos) {
		float chunkSizePx = (float)(Chunk.Size * 16);
		Vector2I centerChunk = new Vector2I(
			Mathf.FloorToInt(worldPos.X / chunkSizePx),
			Mathf.FloorToInt(worldPos.Y / chunkSizePx)
		);

		int maxGensThisFrame = 10; 
		int currentGens = 0;
		
		Vector2I chunkOffset = new Vector2I();
		Vector2I targetCPos = new Vector2I();
		for (int r = 0; r <= SimulationRadius; r++) {
			for (int x = -r; x <= r; x++) {
				for (int y = -r; y <= r; y++) {
					if (Math.Max(Math.Abs(x), Math.Abs(y)) == r) {
						chunkOffset.X = x;
						chunkOffset.Y = y;
						targetCPos = centerChunk + chunkOffset;
						if (!_chunks.ContainsKey(targetCPos)) {
							GenerateChunk(targetCPos);
							currentGens++;
							if (currentGens >= maxGensThisFrame) return;
						}
					}
				}
			}
		}
	}

	private void GenerateChunk(Vector2I cPos) {
		Chunk newChunk = new Chunk(cPos, Generator);
		if (_chunkCache.ContainsKey(cPos)) {
			newChunk.Grid = (int[,])_chunkCache[cPos].Clone();
		}
		else {
			for (int x = 0; x < Chunk.Size; x++) {
				int worldX = cPos.X * Chunk.Size + x;
				int surfaceY = Generator.GetSurfaceHeight(worldX);
					for (int y = 0; y < Chunk.Size; y++) {
					int worldY = cPos.Y * Chunk.Size + y;
					int tileID = Generator.GenerateTile(worldX, worldY, surfaceY);
					
					if(Generator.GenerateBackgroundTile(worldX, worldY, surfaceY) && worldY > surfaceY) {
						SetBackgroundTile(worldX, worldY,1);
					}
					TileProperty prop = _getTileData(tileID);
					newChunk.Grid[x, y] = tileID;
					if (prop.Type != TileType.STATIC && prop.Type != TileType.AIR) {
					newChunk.ActiveTiles.Add(new Vector2I(x, y));
					}
					int tileIDAbove = Generator.GenerateTile(worldX, worldY - 1, surfaceY);
					if(tileID == earthTileID && tileIDAbove == 0 && worldY <= surfaceY) {
						newChunk.OvergrowableTiles.Add(new Vector2I(x, y));
					}
				}
			}
		}
		_chunks.Add(cPos, newChunk);		
		DrawChunk(newChunk);
	}

	public void UnloadFarChunks(Vector2 worldPos) {
		int chunkSizePx = Chunk.Size * 16;
		Vector2I playerChunkPos = new Vector2I(
			Mathf.FloorToInt(worldPos.X / chunkSizePx),
			Mathf.FloorToInt(worldPos.Y / chunkSizePx)
		);
			List<Vector2I> chunksToRemove = new List<Vector2I>();
			foreach (var cPos in _chunks.Keys) {
			int diffX = Math.Abs(cPos.X - playerChunkPos.X);
			int diffY = Math.Abs(cPos.Y - playerChunkPos.Y);
			if (diffX > UnloadRadius || diffY > UnloadRadius) chunksToRemove.Add(cPos);
		}
			foreach (var cPos in chunksToRemove) {
			RemoveChunk(cPos);
		}
	}
		
		
	private void RemoveChunk(Vector2I cPos) {
			if (_chunks.TryGetValue(cPos, out Chunk chunk)) {
				_chunkCache[cPos] = (int[,])chunk.Grid.Clone();
				for (int x = 0; x < Chunk.Size; x++) {
					for (int y = 0; y < Chunk.Size; y++) {
						Vector2I worldTilePos = new Vector2I(
						cPos.X * Chunk.Size + x,
						cPos.Y * Chunk.Size + y
						);
						TargetLayer.SetCell(worldTilePos, -1); 
					}
				}
				_chunks.Remove(cPos);
				}
	}
	
		
	// use this as few as possible to relieve cpu 
	private void DrawChunk(Chunk chunk) {
		for (int x = 0; x < Chunk.Size; x++) {
			for (int y = 0; y < Chunk.Size; y++) {
				int val = chunk.Grid[x, y];
				TileProperty prop = _getTileData(val);
				if (prop.Type != TileType.AIR) {
					Vector2I worldPos = new Vector2I(
						chunk.ChunkPos.X * Chunk.Size + x, 
						chunk.ChunkPos.Y * Chunk.Size + y
					);
					UpdateTileVisual(worldPos, val);
				}
			}
		}
		chunk.IsDirty = false;
	}
			
	/* ################## TILES ################## */	

	public int GetTile(int x, int y) {
		if (y >= Generator.MapHeight) return bedrockTileID; 
		if (y < 0) return airTileID;
		Vector2I cPos = new Vector2I(
			Mathf.FloorToInt((float)x / Chunk.Size), 
			Mathf.FloorToInt((float)y / Chunk.Size)
		);

		if (!_chunks.TryGetValue(cPos, out Chunk chunk)) {
			return airTileID; 
		}

		int localX = (x % Chunk.Size + Chunk.Size) % Chunk.Size;
		int localY = (y % Chunk.Size + Chunk.Size) % Chunk.Size;
		
		return chunk.Grid[localX, localY];
	}

	public void SetTile(int x, int y, int value) {
		Vector2I cPos = new Vector2I(Mathf.FloorToInt((float) x / Chunk.Size), Mathf.FloorToInt((float)y / Chunk.Size));
		if (!_chunks.TryGetValue(cPos, out Chunk chunk)) return;

		int localX = (x % Chunk.Size + Chunk.Size) % Chunk.Size;
		int localY = (y % Chunk.Size + Chunk.Size) % Chunk.Size;

		if (chunk.Grid[localX, localY] != value) {
			int oldID = chunk.Grid[localX, localY];
			chunk.Grid[localX, localY] = value;
			chunk.IsDirty = true;
			
			if(oldID == earthTileID) {
				chunk.OvergrowableTiles.Remove(new Vector2I(localX, localY));
			}
			
			TileProperty prop = _getTileData(value);
			if (prop.Type == TileType.AIR) {
				SetBackgroundTile(x,y,earthTileID); // TODO: fill _backgroundTileData with values matching _tileData
				WakeUpNeighbors(x, y);
				TryAddOvergrowable(x,y + 1);
			}
			else if (prop.Type == TileType.LIQUID) {
				chunk.ActiveTiles.Add(new Vector2I(localX, localY));
			}
			else if(value == earthTileID){
				if (_getTileData(GetTile(x, y - 1)).Type == TileType.AIR) {
				chunk.OvergrowableTiles.Add(new Vector2I(localX, localY));
				}
			}
			UpdateTileVisual(new Vector2I(x, y), value);
		}
	}	
	
	private TileProperty _getTileData(int value) {
		if(_tileData.ContainsKey(value))
			return _tileData[value];
			// fallback in case value is missing
		else return new TileProperty {Type = TileType.AIR};
	}
	
	/* ################## VISUALS ################## */
	
	private void SetBackgroundTile(int x, int y,int oldTileID) {
		if (_backgroundTileData.TryGetValue(oldTileID, out TileProperty prop)) {		
			Background.SetCell(new Vector2I(x,y),prop.AtlasID,prop.AtlasCoords);
		}
	}

	private void UpdateTileVisual(Vector2I worldPos, int value) {
		if (_tileData.TryGetValue(value, out TileProperty prop)) {
		if(prop.Type == TileType.AIR) TargetLayer.SetCell(worldPos, -1);
		else TargetLayer.SetCell(worldPos, prop.AtlasID, prop.AtlasCoords);
		}
	}

		/* ################## SIMULATION ################## */
	
	public void SimulateStep() {
		int tilesProcessed = 0;		
		foreach (var chunk in _chunks.Values) {
			if (chunk.ActiveTiles.Count == 0 && chunk.OvergrowableTiles.Count == 0) continue;

			_activeBuffer.Clear();
			_activeBuffer.AddRange(chunk.ActiveTiles);
			chunk.ActiveTiles.Clear();
			foreach (var localPos in _activeBuffer) {
				if(tilesProcessed >= maxTilesPerTick) {
					chunk.ActiveTiles.Add(localPos);
					continue;
				}
				int currentID = chunk.Grid[localPos.X, localPos.Y];
				TileProperty prop = _getTileData(currentID);
				
				// liquid physics
				if (prop.Type == TileType.LIQUID) {
					if(_rng.NextDouble() < prop.Viscosity) {
					SimulateLiquid(chunk, localPos, currentID);
					tilesProcessed++;
						}
					}
			}
		}
	}
	
	private void SimulateLiquid(Chunk chunk, Vector2I localPos, int id) {
		int worldX = chunk.ChunkPos.X * Chunk.Size + localPos.X;
		int worldY = chunk.ChunkPos.Y * Chunk.Size + localPos.Y;
		
		// fall down
		if (_getTileData(GetTile(worldX, worldY + 1)).Type == TileType.AIR) {
			SetTile(worldX, worldY, airTileID);
			SetTile(worldX, worldY + 1, id);
		} 
		// sideways
		else {
			int sideDir = _rng.Next(2) == 0 ? 1 : -1;
			if (_getTileData(GetTile(worldX + sideDir, worldY)).Type == TileType.AIR) {
				SetTile(worldX, worldY, airTileID);
				SetTile(worldX + sideDir, worldY, id);
			}
			else if (_getTileData(GetTile(worldX - sideDir, worldY)).Type == TileType.AIR) {
				SetTile(worldX, worldY, airTileID);
				SetTile(worldX - sideDir, worldY, id);
			}
		}
	}
	private void SimulateVegetation() {
		int tilesProcessed = 0;	
		foreach (var chunk in _chunks.Values) {
			if (chunk.OvergrowableTiles.Count == 0) continue;
			
			_vegetationBuffer.Clear();

			int count = 0;
			foreach(var localPos in chunk.OvergrowableTiles) {
				if(count >= VegetationLimit) break;
				_vegetationBuffer.Add(localPos);
				count++;
			}
			foreach (var localPos in _vegetationBuffer){
				if(tilesProcessed >= maxTilesPerTick) {
					continue;
				}
				int worldX = chunk.ChunkPos.X * Chunk.Size + localPos.X;
				int worldY = chunk.ChunkPos.Y * Chunk.Size + localPos.Y;
				
				if (_getTileData(GetTile(worldX, worldY - 1)).Type == TileType.AIR) {
				if (_rng.Next(2) == 0) {
					SetTile(worldX, worldY, grassTileID);
					tilesProcessed++;
					chunk.OvergrowableTiles.Remove(localPos);
					}
				}	
			}
		}
	}	
	
	private void TryAddOvergrowable(int worldX, int worldY) {
		if (GetTile(worldX, worldY) == earthTileID) {
			Vector2I cPos = new Vector2I(Mathf.FloorToInt((float)worldX / Chunk.Size), Mathf.FloorToInt((float)worldY / Chunk.Size));
			if (_chunks.TryGetValue(cPos, out Chunk chunk)) {
				Vector2I lPos = new Vector2I((worldX % Chunk.Size + Chunk.Size) % Chunk.Size, (worldY % Chunk.Size + Chunk.Size) % Chunk.Size);
				chunk.OvergrowableTiles.Add(lPos);
				}
			}
	}	

	private void WakeUpNeighbors(int x, int y) {
		int[] dx = { 0, 0, -1, 1, 0 };
		int[] dy = { -1, 1, 0, 0, 0 }; 
		for(int i = 0; i < 5; i++) {
		ActivateTile(x + dx[i], y + dy[i]);
		}
	}

	private void ActivateTile(int x, int y) {
		Vector2I cPos = new Vector2I(Mathf.FloorToInt((float)x / Chunk.Size), Mathf.FloorToInt((float)y / Chunk.Size));
		if (_chunks.TryGetValue(cPos, out Chunk chunk)) {
			int lx = (x % Chunk.Size + Chunk.Size) % Chunk.Size;
			int ly = (y % Chunk.Size + Chunk.Size) % Chunk.Size;
			int val = chunk.Grid[lx, ly];
			TileProperty prop = _getTileData(val);
			if (prop.Type == TileType.LIQUID) chunk.ActiveTiles.Add(new Vector2I(lx, ly));
		}
	}
	
	
	public override void _Ready() {
		bool isReady = true;
		if (Generator == null) {
			isReady = false;
			}
		if (TargetLayer == null) {
			isReady = false;
			}
		if (isReady) {
			UpdateChunksAround(Vector2.Zero);
			}
		}

	public override void _Process(double delta) {
		var camera = GetViewport().GetCamera2D();
		if (camera == null) return;
		
		UpdateChunksAround(camera.GlobalPosition);
		
		_cleanupTimer += delta;
		_timer += delta;
		_overgrowTimer += delta;
		if(_timer >= TickRate) {
			SimulateStep();
			_timer = 0;
		}
		if(_cleanupTimer > CleanupRate) {
			UnloadFarChunks(camera.GlobalPosition);
			_cleanupTimer = 0;
		}
		if(_overgrowTimer >= OvergrowRate){
			SimulateVegetation();
			_overgrowTimer = 0;
		}
	}
	
	/* ################## HELPERS ################## */
	
	private Vector2i GetWorldPos(){
		return new Vector2I(0,0);
	}
	private Vector2i GetLocalPos(){
		return new Vector2I(0,0);
	}
}
