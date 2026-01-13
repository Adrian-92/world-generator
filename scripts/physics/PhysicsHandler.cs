using Godot;
using System;
using System.Collections.Generic;
using static GlobalConstants;
using static BiomeData;
public partial class PhysicsHandler : Node
{
	[Export] public WorldGenerator Generator;
	[Export] public TileMapLayer TargetLayer;
	[Export] public TileMapLayer Background;
	[Export] public double TickRate = 0.5;
	[Export] public int SimulationRadius = 5;
	[Export] public int UnloadRadius = 10;
	[Export] public int VegetationLimit = 8;
	[Export] public double OvergrowRate = 10;
	
	private const int AirID = 0;
	private const int EarthID = 1;
	
	private System.Random _rng = new System.Random();
	private double _timer = 0;
	private double _overgrowTimer = 0;
	private double _cleanupTimer = 0;
	private double CleanupRate = 5.0;
	
	private const int overgrowChance = 10;
	

	private List<Vector2I> _activeBuffer = new List<Vector2I>();
	private List<Vector2I> _vegetationBuffer = new List<Vector2I>();
	private Dictionary<Vector2I, Chunk> _chunks = new Dictionary<Vector2I, Chunk>();
	private Dictionary<Vector2I, Tile[,]> _chunkCache = new Dictionary<Vector2I, Tile[,]>();

	public override void _Ready() {
		TileRegistry.LoadAll();
		if (Generator != null && TargetLayer != null) {
			UpdateChunksAround(Vector2.Zero);
		}
	}

	public override void _Process(double delta) {
		var camera = GetViewport().GetCamera2D();
		if (camera == null) return;
		
		UpdateChunksAround(camera.GlobalPosition);
		
		_timer += delta;
		_overgrowTimer += delta;
		_cleanupTimer += delta;

		if(_timer >= TickRate) {
			SimulateStep();
			_timer = 0;
		}

		if(_overgrowTimer >= OvergrowRate) {
			SimulateVegetation();
			_overgrowTimer = 0;
		}

		if(_cleanupTimer >= CleanupRate) {
			UnloadFarChunks(camera.GlobalPosition);
			_cleanupTimer = 0;
		}
	}

	/* ################## CHUNK LOGIC ################## */

	public void UpdateChunksAround(Vector2 worldPos) {
		float chunkSizePx = (float)(Chunk.Size * 16);
		Vector2I centerChunk = new Vector2I(
			Mathf.FloorToInt(worldPos.X / chunkSizePx),
			Mathf.FloorToInt(worldPos.Y / chunkSizePx)
		);

		int maxGensThisFrame = 50; 
		int currentGens = 0;

		for (int r = 0; r <= SimulationRadius; r++) {
			for (int x = -r; x <= r; x++) {
				for (int y = -r; y <= r; y++) {
					if (Math.Max(Math.Abs(x), Math.Abs(y)) == r) {
						Vector2I targetCPos = centerChunk + new Vector2I(x, y);
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
		if (_chunks.ContainsKey(cPos)) return;

		Chunk newChunk = new Chunk();
		newChunk.Name = $"Chunk_{cPos.X}_{cPos.Y}";
		AddChild(newChunk);
		
		if (_chunkCache.TryGetValue(cPos, out Tile[,] cachedGrid)) {
			newChunk.InitializeFromCache(cPos, TargetLayer.TileSet, cachedGrid, Generator);
		} else {
			newChunk.Initialize(cPos, TargetLayer.TileSet, Generator);
		}
		
		_chunks.Add(cPos, newChunk);
	}

	public void UnloadFarChunks(Vector2 worldPos) {
		float chunkSizePx = Chunk.Size * 16f;
		Vector2I playerChunkPos = new Vector2I(
			Mathf.FloorToInt(worldPos.X / chunkSizePx),
			Mathf.FloorToInt(worldPos.Y / chunkSizePx)
		);

		List<Vector2I> toRemove = new List<Vector2I>();

		foreach (var cPos in _chunks.Keys) {
			if (Math.Abs(cPos.X - playerChunkPos.X) > UnloadRadius || Math.Abs(cPos.Y - playerChunkPos.Y) > UnloadRadius) {
				toRemove.Add(cPos);
			}
		}
		foreach (var cPos in toRemove) {
			Chunk chunkNode = _chunks[cPos];
			_chunkCache[cPos] = (Tile[,])chunkNode.Grid.Clone();
			chunkNode.QueueFree();
			_chunks.Remove(cPos);
		}
	}

	/* ################## TILES ################## */

	public int GetTile(int x, int y) {
		if (y >= Generator.MapHeight) return 12; // Bedrock ID
		if (y < 0) return AirID;
		
		Vector2I cPos = WorldToChunkPos(x, y);
		if (!_chunks.TryGetValue(cPos, out Chunk chunk)) return AirID;
		
		return chunk.Grid[Chunk.ToLocal(x), Chunk.ToLocal(y)].TileID;
	}

	public void SetTile(int worldX, int worldY, int newTileID) {
		Vector2I cPos = WorldToChunkPos(worldX, worldY);
		if (!_chunks.TryGetValue(cPos, out Chunk chunk)) return;

		Vector2I lPos = WorldToLocalPos(worldX, worldY);
		int oldID = chunk.Grid[lPos.X, lPos.Y].TileID;
		if (oldID == newTileID) return;

		var prop = TileRegistry.Get(newTileID);
		Tile newTile = new Tile((short) newTileID, (short) prop.MaxHealth, prop.Type, prop.Viscosity, (short) prop.DamageOnContact);

		chunk.Grid[lPos.X, lPos.Y] = newTile;

		if (oldID == EarthID) chunk.OvergrowableTiles.Remove(lPos);
	   
		if (newTile.Type == TileType.LIQUID) chunk.ActiveTiles.Add(lPos);

		int surfaceY = Generator.GetSurfaceHeight(worldX);
		chunk.DrawTile(lPos.X, lPos.Y, newTile, worldX, worldY, surfaceY, Generator);
		
		if (newTile.Type == TileType.AIR) {
			WakeUpNeighbors(worldX, worldY);
			TryAddOvergrowable(worldX, worldY + 1); 
		}
	}



	/* ################## SIMULATION ################## */

	public void SimulateStep() {
		foreach (var chunk in _chunks.Values) {
			chunk.Tick(this);
		}
	}

	public void SimulateLiquid(Chunk chunk, Vector2I localPos, Tile tile) {
			int worldX = chunk.ChunkPos.X * Chunk.Size + localPos.X;
			int worldY = chunk.ChunkPos.Y * Chunk.Size + localPos.Y;

			if (GetTile(worldX, worldY + 1) == AirID) {
				SetTile(worldX, worldY, AirID);
				SetTile(worldX, worldY + 1, tile.TileID);
			} else {
				int sideDir = _rng.Next(2) == 0 ? 1 : -1;
				if (GetTile(worldX + sideDir, worldY) == AirID) {
					SetTile(worldX, worldY, AirID);
					SetTile(worldX + sideDir, worldY, tile.TileID);
				}
			}
		}

private void SimulateVegetation() {
	foreach (var chunk in _chunks.Values) {
		if (chunk.OvergrowableTiles.Count == 0) continue;

		_vegetationBuffer.Clear();
		int count = 0;
		
		foreach (var pos in chunk.OvergrowableTiles) {
			if (count++ >= VegetationLimit){
				_vegetationBuffer.Add(pos);
				break;
			} 
		}
		foreach (var localPos in _vegetationBuffer) {
			int worldX = chunk.ChunkPos.X * Chunk.Size + localPos.X;
			int worldY = chunk.ChunkPos.Y * Chunk.Size + localPos.Y;

			if (TileRegistry.Get(GetTile(worldX, worldY - 1)).Type == TileType.AIR) {
				if (_rng.Next(overgrowChance) == 0) {
					// ID 2 ist Gras aktuell
					SetTile(worldX, worldY, 2); 
				}
			} else {

				chunk.OvergrowableTiles.Remove(localPos);
			}
		}
	}
}

	/* ################## HELPERS ################## */
	private Vector2I WorldToChunkPos(int x, int y) => new Vector2I(Mathf.FloorToInt((float)x / Chunk.Size), Mathf.FloorToInt((float)y / Chunk.Size));
	private Vector2I WorldToLocalPos(int x, int y) => new Vector2I((x % Chunk.Size + Chunk.Size) % Chunk.Size, (y % Chunk.Size + Chunk.Size) % Chunk.Size);



	private void WakeUpNeighbors(int x, int y) {
		ActivateTile(x + 1, y); ActivateTile(x - 1, y);
		ActivateTile(x, y + 1); ActivateTile(x, y - 1);
	}

	private void ActivateTile(int x, int y) {
		Vector2I cPos = WorldToChunkPos(x, y);
		if (_chunks.TryGetValue(cPos, out Chunk chunk)) {
			Vector2I lPos = WorldToLocalPos(x, y);
			if (chunk.Grid[lPos.X, lPos.Y].Type == TileType.LIQUID) chunk.ActiveTiles.Add(lPos);
		}
	}
	private void TryAddOvergrowable(int x, int y) {
		if (GetTile(x, y) == EarthID) {
			Vector2I cPos = WorldToChunkPos(x, y);
			if (_chunks.TryGetValue(cPos, out Chunk chunk)) {
				Vector2I lPos = WorldToLocalPos(x, y);
				chunk.OvergrowableTiles.Add(lPos);
			}
		}
	}
}
