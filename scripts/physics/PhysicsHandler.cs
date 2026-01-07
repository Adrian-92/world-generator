using Godot;
using System;
using System.Collections.Generic;
using static GlobalConstants;

public partial class PhysicsHandler : Node
{
	[Export] public WorldGenerator Generator;
	[Export] public TileMapLayer TargetLayer;
	[Export] public double TickRate = 0.1;
	[Export] public double CleanupRate = 5;
	[Export] public int SimulationRadius = 5;
	[Export] public int UnloadRadius = 10;
	
	private System.Random _rng = new System.Random();
	private double _timer = 0;
	private double _cleanupTimer = 0;

	
	private Dictionary<Vector2I, Chunk> _chunks = new Dictionary<Vector2I, Chunk>();
	private Dictionary<Vector2I, int[,]> _chunkCache = new Dictionary<Vector2I, int[,]>();
	
	private Dictionary<int, Vector2I> _idToAtlas = new Dictionary<int, Vector2I>() {
	{ 0, new Vector2I(0, 0) },  // Gras (Hellgrün)
	{ 1, new Vector2I(1, 0) },  // Erde
	{ 2, new Vector2I(15, 0) }, // Wasser (Ganz rechts)
	{ 3, new Vector2I(6, 0) },  // Magma (Rot)
	{ 4, new Vector2I(2, 0) },  // Dunkle Erde
	{ 5, new Vector2I(3, 0) },  // Stein (Grau)
	{ 6, new Vector2I(4, 0) },  // Dunkler Stein
	{ 7, new Vector2I(7, 0) },  
	{ 8, new Vector2I(8, 0) },
	{ 9, new Vector2I(9, 0) },
	{ 10, new Vector2I(10, 0) },
	{ 11, new Vector2I(11, 0) },
	{ 12, new Vector2I(12, 0) },
	{ 13, new Vector2I(13, 0) },
	{ 14, new Vector2I(14, 0) }
};

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
				// Nur den äußeren Rand des aktuellen Quadranten prüfen
				if (Math.Max(Math.Abs(x), Math.Abs(y)) == r) {
					Vector2I targetCPos = centerChunk + new Vector2I(x, y);

					if (!_chunks.ContainsKey(targetCPos)) {
						GenerateChunk(targetCPos);
						currentGens++;

						// WICHTIG: Wenn wir unser Limit erreicht haben, 
						// beenden wir die Funktion für DIESEN Frame.
						if (currentGens >= maxGensThisFrame) return;
					}
				}
			}
		}
	}
}

	private void GenerateChunk(Vector2I cPos) {
		GD.Print($"Lade Chunk: {cPos}");
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
					
					newChunk.Grid[x, y] = (tileID == -1) ? 999 : tileID;
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

		foreach (var cPos in chunksToRemove)
		{
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

	public void SimulateStep() {
		foreach (var chunk in _chunks.Values) {
			for (int x = 0; x < Chunk.Size; x++) {
				for (int y = Chunk.Size - 1; y >= 0; y--) {
					int current = chunk.Grid[x, y];
					
					// Nur Wasser (2) oder Lava (3) simulieren
					if (current != 2 && current != 3) continue;

					int worldX = chunk.ChunkPos.X * Chunk.Size + x;
					int worldY = chunk.ChunkPos.Y * Chunk.Size + y;

					// 1. Check: Kann es nach unten fallen?
					if (GetTile(worldX, worldY + 1) == 999) {
						SetTile(worldX, worldY, 999);
						SetTile(worldX, worldY + 1, current);
					} 
					else {
						int sideDir = _rng.Next(2) == 0 ? 1 : -1;
						
						if (GetTile(worldX + sideDir, worldY) == 999) {
							SetTile(worldX, worldY, 999);
							SetTile(worldX + sideDir, worldY, current);
						}
						else if (GetTile(worldX - sideDir, worldY) == 999) {
							SetTile(worldX, worldY, 999);
							SetTile(worldX - sideDir, worldY, current);
						}
					}
				}
			}
			if (chunk.IsDirty) DrawChunk(chunk);
		}
	}

	public int GetTile(int x, int y) {
		Vector2I cPos = new Vector2I(
			Mathf.FloorToInt((float)x / Chunk.Size), 
			Mathf.FloorToInt((float)y / Chunk.Size)
		);

		if (!_chunks.TryGetValue(cPos, out Chunk chunk)) {
			return 1; 
		}

		int localX = (x % Chunk.Size + Chunk.Size) % Chunk.Size;
		int localY = (y % Chunk.Size + Chunk.Size) % Chunk.Size;
		
		return chunk.Grid[localX, localY];
}

	public void SetTile(int x, int y, int value) {
		Vector2I cPos = new Vector2I(Mathf.FloorToInt((float)x / Chunk.Size), Mathf.FloorToInt((float)y / Chunk.Size));
		if (!_chunks.TryGetValue(cPos, out Chunk chunk)) return;

		int localX = (x % Chunk.Size + Chunk.Size) % Chunk.Size;
		int localY = (y % Chunk.Size + Chunk.Size) % Chunk.Size;

		if (chunk.Grid[localX, localY] != value) {
			chunk.Grid[localX, localY] = value;
			chunk.IsDirty = true;
			UpdateTileVisual(new Vector2I(x, y), value);
		}
	}

	private void DrawChunk(Chunk chunk) {
		for (int x = 0; x < Chunk.Size; x++) {
			for (int y = 0; y < Chunk.Size; y++) {
				Vector2I worldPos = new Vector2I(chunk.ChunkPos.X * Chunk.Size + x, chunk.ChunkPos.Y * Chunk.Size + y);
				UpdateTileVisual(worldPos, chunk.Grid[x, y]);
			}
		}
		chunk.IsDirty = false;
	}

	private void UpdateTileVisual(Vector2I worldPos, int value) {
		if (value == 999) TargetLayer.SetCell(worldPos, -1);
		else if (_idToAtlas.ContainsKey(value)) TargetLayer.SetCell(worldPos, 1, _idToAtlas[value]);
	}
	
	
public override void _Ready()
{
	bool isReady = true;

	if (Generator == null)
	{
		isReady = false;
	}

	if (TargetLayer == null)
	{
		isReady = false;
	}

	if (isReady)
	{
		GD.Print("PhysicsHandler: Alles bereit. Welt-Initialisierung gestartet.");
		UpdateChunksAround(Vector2.Zero);
	}
}

	public override void _Process(double delta) {
		var camera = GetViewport().GetCamera2D();
		if (camera != null)
		{
		UpdateChunksAround(camera.GlobalPosition);
		}
		_cleanupTimer += delta;
		_timer += delta;
		if(_timer >= TickRate) {
			SimulateStep();
			_timer = 0;
		}
		if(_cleanupTimer > CleanupRate){
			UnloadFarChunks(camera.GlobalPosition);
			_cleanupTimer = 0;
		}
	}
}
