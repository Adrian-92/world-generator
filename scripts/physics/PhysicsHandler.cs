using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using static GlobalConstants;
using static BiomeData;
public partial class PhysicsHandler : Node
{
	[Export] public WorldGenerator Generator;
	[Export] public TileMapLayer TargetLayer;
	[Export] public TileMapLayer Background;
	[Export] public double TickRate = 0.5;
	[Export] public int SimulationRadius = 5;
	[Export] public int MaxUpdatesPerFrame = 2000;
	[Export] public int UnloadRadius = 10;
	[Export] public int VegetationLimit = 8;
	[Export] public double OvergrowRate = 10;
	
	private const int AirID = 0;
	private const int EarthID = 1;
	
	private double _timer = 0;
	private double _overgrowTimer = 0;
	private double _cleanupTimer = 0;
	private double CleanupRate = 5.0;
	
	private const int overgrowChance = 10;
	
	private bool _isGenerating = false;
	private bool _isVegetating = false;
	private bool _isSimulating = false;
	private bool _isCleaningUp = false;
	
	private CancellationTokenSource _cts = new CancellationTokenSource();
	
	private List<Vector2I> _activeBuffer = new List<Vector2I>();
	private List<Vector2I> _vegetationBuffer = new List<Vector2I>();
	private ConcurrentDictionary<Vector2I, Chunk> _chunks = new ConcurrentDictionary<Vector2I, Chunk>();
	private ConcurrentDictionary<Vector2I, Tile[,]> _chunkCache = new ConcurrentDictionary<Vector2I, Tile[,]>();	
	
	private struct TileVisualUpdate {
		public int WorldX;
		public int WorldY;
	}
	
	private ConcurrentQueue<TileVisualUpdate> _visualUpdates = new ConcurrentQueue<TileVisualUpdate>();
	
	public override void _ExitTree() {
		_cts.Cancel();        
		_cts.Dispose();
	}
	
	public override void _Ready() {
		TileRegistry.LoadAll();
		if (Generator != null && TargetLayer != null) {
			UpdateChunksAround(Vector2.Zero);
		}

	}

	public override void _Process(double delta) {
		var camera = GetViewport().GetCamera2D();
		if (camera == null) return;
		if (_cts.IsCancellationRequested) return;
		if(!_isGenerating) {
			_isGenerating = true;
			Vector2 camPos = camera.GlobalPosition;
			Task.Run(() => {
			UpdateChunksAround(camPos);
			_isGenerating = false;
				});
			}
		
		_timer += delta;
		_overgrowTimer += delta;
		_cleanupTimer += delta;

		_processVisualUpdates();
		
		if(_timer >= TickRate) {
			if(!_isSimulating){
				_isSimulating = true;
				Task.Run(() => {
				SimulateStep();
				_isSimulating = false;
				});	
			}
			_timer = 0;
		}

		if(_overgrowTimer >= OvergrowRate) {
			if(!_isVegetating) {
				_isVegetating = true;
				Task.Run(() => {
					SimulateVegetation();
					_isVegetating = false;
				});
			}
			
			_overgrowTimer = 0;
		}

		if(_cleanupTimer >= CleanupRate) {
			if(!_isCleaningUp) {
				_isCleaningUp = true;
				Vector2 camPos = camera.GlobalPosition; 
				Task.Run(() => {
				UnloadFarChunks(camPos);
					_isCleaningUp = false;
				});
			}
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
					if (_cts.IsCancellationRequested) return;
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

		
		if (_chunkCache.TryGetValue(cPos, out Tile[,] cachedGrid)) {
			newChunk.InitializeFromCache(cPos, TargetLayer.TileSet, cachedGrid, Generator);
		} else {
			newChunk.Initialize(cPos, TargetLayer.TileSet, Generator);
		}
		if(_chunks.TryAdd(cPos, newChunk)) {
			CallDeferred(nameof(AddChunkNode), newChunk);		
		}
		else {
			newChunk.QueueFree();
		}
		
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
			if (_chunks.TryRemove(cPos, out Chunk chunkNode)) {
				_chunkCache[cPos] = (Tile[,])chunkNode.Grid.Clone();
				chunkNode.CallDeferred(Node.MethodName.QueueFree);
			}
		}
	}
	
	// 
	private void AddChunkNode(Chunk newChunk) {
		AddChild(newChunk);	
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
		
		lock (chunk) {
			int oldID = chunk.Grid[lPos.X, lPos.Y].TileID;
			if (oldID == newTileID) return;

			var prop = TileRegistry.Get(newTileID);
			Tile newTile = new Tile((short) newTileID, (short) prop.MaxHealth, prop.Type, prop.Viscosity, (short) prop.DamageOnContact);

			chunk.Grid[lPos.X, lPos.Y] = newTile;

			if (oldID == EarthID) chunk.OvergrowableTiles.Remove(lPos);
			if (newTile.Type == TileType.LIQUID) chunk.ActiveTiles.Add(lPos);

			int surfaceY = Generator.GetSurfaceHeight(worldX);
			
			if (newTile.Type == TileType.AIR) {
				WakeUpNeighbors(worldX, worldY);
				TryAddOvergrowable(worldX, worldY + 1); 
			}
		}
		_visualUpdates.Enqueue(new TileVisualUpdate { WorldX = worldX, WorldY = worldY });
	}

	private void _processVisualUpdates() {
		int processed = 0;
		while(processed < MaxUpdatesPerFrame && _visualUpdates.TryDequeue(out TileVisualUpdate update)) {
			Vector2I cPos = WorldToChunkPos(update.WorldX, update.WorldY);
			if (_chunks.TryGetValue(cPos, out Chunk chunk)) {
				Vector2I lPos = WorldToLocalPos(update.WorldX, update.WorldY);
				
				Tile currentTile = chunk.Grid[lPos.X, lPos.Y];
				int surfaceY = Generator.GetSurfaceHeight(update.WorldX);
				
				chunk.DrawTile(lPos.X, lPos.Y, currentTile, update.WorldX, update.WorldY, surfaceY, Generator);
			}
			processed++;
		}
	}


	/* ################## SIMULATION ################## */

	public void SimulateStep() {
		foreach (var chunk in _chunks.Values) {
			if (_cts.IsCancellationRequested) return;
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
				int sideDir = Random.Shared.Next(2) == 0 ? 1 : -1;
				if (GetTile(worldX + sideDir, worldY) == AirID) {
					SetTile(worldX, worldY, AirID);
					SetTile(worldX + sideDir, worldY, tile.TileID);
				}
			}
		}

private void SimulateVegetation() {
	foreach (var chunk in _chunks.Values) {		
		if (_cts.IsCancellationRequested) return;
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
				if (Random.Shared.Next(overgrowChance) == 0) {
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
