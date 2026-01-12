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
	[Export] public int SimulationRadius = 5;
	[Export] public int UnloadRadius = 10;
	[Export] public int VegetationLimit = 8;
	[Export] public double OvergrowRate = 10;
	
	private System.Random _rng = new System.Random();
	private double _timer = 0;
	private double _overgrowTimer = 0;
	private double _cleanupTimer = 0;
	private double CleanupRate = 5.0;
	
	private const int airTileID = 0;
	private const int earthTileID = 1;
	private const int grassTileID = 2;
	private const int bedrockTileID = 12;
	private const int maxTilesPerTick = 1000;

	private List<Vector2I> _activeBuffer = new List<Vector2I>();
	private List<Vector2I> _vegetationBuffer = new List<Vector2I>();
	private Dictionary<Vector2I, Chunk> _chunks = new Dictionary<Vector2I, Chunk>();
	private Dictionary<Vector2I, Tile[,]> _chunkCache = new Dictionary<Vector2I, Tile[,]>();

	public override void _Ready() {
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

		int maxGensThisFrame = 10; 
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
		Chunk newChunk = new Chunk(cPos, Generator);
		if (_chunkCache.ContainsKey(cPos)) {
			newChunk.Grid = (Tile[,])_chunkCache[cPos].Clone();
		}
		else {
			for (int x = 0; x < Chunk.Size; x++) {
				int worldX = cPos.X * Chunk.Size + x;
				int surfaceY = Generator.GetSurfaceHeight(worldX);
				for (int y = 0; y < Chunk.Size; y++) {
					int worldY = cPos.Y * Chunk.Size + y;
					Tile genTile = Generator.GenerateTile(worldX, worldY, surfaceY);
					newChunk.Grid[x, y] = genTile;
					if (genTile.HasBackground && worldY > surfaceY) {
						SetBackgroundTile(worldX, worldY, 1);
					}
					if (genTile.Type != TileType.STATIC && genTile.Type != TileType.AIR) {
						newChunk.ActiveTiles.Add(new Vector2I(x, y));
					}
					if (genTile.TileID == earthTileID && worldY <= surfaceY + 2) {
						if (Generator.GenerateTile(worldX, worldY - 1, surfaceY).Type == TileType.AIR) {
							newChunk.OvergrowableTiles.Add(new Vector2I(x, y));
						}
					}
				}
			}
		}
		_chunks.Add(cPos, newChunk);
		DrawChunk(newChunk);
	}

	public void UnloadFarChunks(Vector2 worldPos) {
		Vector2I playerChunkPos = WorldToChunkPos((int)worldPos.X / 16, (int)worldPos.Y / 16);
		List<Vector2I> toRemove = new List<Vector2I>();

		foreach (var cPos in _chunks.Keys) {
			if (Math.Abs(cPos.X - playerChunkPos.X) > UnloadRadius || Math.Abs(cPos.Y - playerChunkPos.Y) > UnloadRadius) {
				toRemove.Add(cPos);
			}
		}

		foreach (var cPos in toRemove) {
			_chunkCache[cPos] = (Tile[,])_chunks[cPos].Grid.Clone();
			// Zellen in TileMap löschen
			for (int x = 0; x < Chunk.Size; x++) {
				for (int y = 0; y < Chunk.Size; y++) {
					TargetLayer.SetCell(new Vector2I(cPos.X * Chunk.Size + x, cPos.Y * Chunk.Size + y), -1);
				}
			}
			_chunks.Remove(cPos);
		}
	}

	/* ################## TILES ################## */

	public int GetTile(int x, int y) {
		if (y >= Generator.MapHeight) return bedrockTileID;
		if (y < 0) return airTileID;
		Vector2I cPos = WorldToChunkPos(x, y);
		if (!_chunks.TryGetValue(cPos, out Chunk chunk)) return airTileID;
		Vector2I lPos = WorldToLocalPos(x, y);
		return chunk.Grid[lPos.X, lPos.Y].TileID;
	}

	public void SetTile(int x, int y, int newTileID) {
		Vector2I cPos = WorldToChunkPos(x, y);
		if (!_chunks.TryGetValue(cPos, out Chunk chunk)) return;
		Vector2I lPos = WorldToLocalPos(x, y);

		if (chunk.Grid[lPos.X, lPos.Y].TileID != newTileID) {
			int oldID = chunk.Grid[lPos.X, lPos.Y].TileID;
			TileProperty prop = _getTileData(newTileID);
			Tile newTile = new Tile((short)newTileID, 10, prop.Type, prop.Viscosity, 0);

			chunk.Grid[lPos.X, lPos.Y] = newTile;
			chunk.IsDirty = true;

			if (oldID == earthTileID) chunk.OvergrowableTiles.Remove(lPos);
			if (newTile.Type == TileType.AIR) {
				SetBackgroundTile(x, y, earthTileID);
				WakeUpNeighbors(x, y);
				TryAddOvergrowable(x, y + 1);
			} else if (newTile.Type == TileType.LIQUID) {
				chunk.ActiveTiles.Add(lPos);
			}
			UpdateTileVisual(new Vector2I(x, y), newTileID);
		}
	}

	private TileProperty _getTileData(int id) {
		if (TileAtlasData.TilesetData.TryGetValue(id, out Vector2I coords)) {
			TileType t = TileType.STATIC;
			if (id == 0) t = TileType.AIR;
			if (id == 3 || id == 4) t = TileType.LIQUID;
			if (id == 12) t = TileType.BEDROCK;
			
			return new TileProperty { 
				Type = t, 
				AtlasCoords = coords, 
				AtlasID = 1, 
				Viscosity = (id == 4) ? 0.2f : 1.0f 
			};
		}
		return new TileProperty { Type = TileType.AIR };
	}

	private void SetBackgroundTile(int x, int y, int oldTileID) {
		if (TileAtlasData.BackgroundTileData.TryGetValue(oldTileID, out Vector2I coords)) {
			Background.SetCell(new Vector2I(x, y), 1, coords);
		}
	}

	private void UpdateTileVisual(Vector2I worldPos, int tileID) {
		if (TileAtlasData.TilesetData.TryGetValue(tileID, out Vector2I coords)) {
			if (tileID == 0) TargetLayer.SetCell(worldPos, -1);
			else TargetLayer.SetCell(worldPos, 1, coords);
		}
	}

	/* ################## SIMULATION ################## */

	public void SimulateStep() {
		int tilesProcessed = 0;
		foreach (var chunk in _chunks.Values) {
			if (chunk.ActiveTiles.Count == 0) continue;
			_activeBuffer.Clear();
			_activeBuffer.AddRange(chunk.ActiveTiles);
			chunk.ActiveTiles.Clear();

			foreach (var localPos in _activeBuffer) {
				if (tilesProcessed >= maxTilesPerTick) {
					chunk.ActiveTiles.Add(localPos);
					continue;
				}
				Tile currentTile = chunk.Grid[localPos.X, localPos.Y];
				if (currentTile.Type == TileType.LIQUID) {
					SimulateLiquid(chunk, localPos, currentTile);
					tilesProcessed++;
				}
			}
		}
	}

	private void SimulateLiquid(Chunk chunk, Vector2I localPos, Tile tile) {
		int worldX = chunk.ChunkPos.X * Chunk.Size + localPos.X;
		int worldY = chunk.ChunkPos.Y * Chunk.Size + localPos.Y;
		if (GetTile(worldX, worldY + 1) == airTileID) {
			SetTile(worldX, worldY, airTileID);
			SetTile(worldX, worldY + 1, tile.TileID);
		} else {
			int sideDir = _rng.Next(2) == 0 ? 1 : -1;
			if (GetTile(worldX + sideDir, worldY) == airTileID) {
				SetTile(worldX, worldY, airTileID);
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
				if (count++ >= VegetationLimit) break;
				_vegetationBuffer.Add(pos);
			}
			foreach (var localPos in _vegetationBuffer) {
				int worldX = chunk.ChunkPos.X * Chunk.Size + localPos.X;
				int worldY = chunk.ChunkPos.Y * Chunk.Size + localPos.Y;
				if (_getTileData(GetTile(worldX, worldY - 1)).Type == TileType.AIR) {
					if (_rng.Next(10) == 0) {
						SetTile(worldX, worldY, grassTileID);
						chunk.OvergrowableTiles.Remove(localPos);
					}
				}
			}
		}
	}

	/* ################## HELPERS ################## */
	private Vector2I WorldToChunkPos(int x, int y) => new Vector2I(Mathf.FloorToInt((float)x / Chunk.Size), Mathf.FloorToInt((float)y / Chunk.Size));
	private Vector2I WorldToLocalPos(int x, int y) => new Vector2I((x % Chunk.Size + Chunk.Size) % Chunk.Size, (y % Chunk.Size + Chunk.Size) % Chunk.Size);

	private void DrawChunk(Chunk chunk) {
		for (int x = 0; x < Chunk.Size; x++) {
			for (int y = 0; y < Chunk.Size; y++) {
				Tile tile = chunk.Grid[x, y];
				if (tile.Type != TileType.AIR) {
					Vector2I worldPos = new Vector2I(chunk.ChunkPos.X * Chunk.Size + x, chunk.ChunkPos.Y * Chunk.Size + y);
					UpdateTileVisual(worldPos, tile.TileID);
				}
			}
		}
	}

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
		if (GetTile(x, y) == earthTileID) {
			Vector2I cPos = WorldToChunkPos(x, y);
			if (_chunks.TryGetValue(cPos, out Chunk chunk)) chunk.OvergrowableTiles.Add(WorldToLocalPos(x, y));
		}
	}
}
