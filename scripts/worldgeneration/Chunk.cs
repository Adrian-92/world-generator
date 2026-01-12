using Godot;
using System;
using System.Collections.Generic;
using static GlobalConstants;
using static BiomeData;

public partial class Chunk : Node2D
{
	public const int Size = 32;
	public const int TilePixelSize = 16;
	public const int MaxUpdates = 100;
	public const int EarthID = 1;

	public static int ToLocal(int worldCoord) {
		int res = worldCoord % Size;
		return res < 0 ? res + Size : res;
	}
	public string Name;
	public Tile[,] Grid;
	public Vector2I ChunkPos;
	public Biome Biome;
	
	public HashSet<Vector2I> ActiveTiles = new HashSet<Vector2I>();
	public HashSet<Vector2I> OvergrowableTiles = new HashSet<Vector2I>();
	
	public bool IsDirty;
	public bool IsActive; 

	public TileMapLayer GroundLayer;
	public TileMapLayer BackgroundLayer;

	public void Initialize(Vector2I cPos, TileSet tileSet, WorldGenerator generator) {
		this.ChunkPos = cPos;
		this.Grid = new Tile[Size, Size];
		
		if (generator != null) {
			this.Biome = generator.GetBiomeAt(cPos.X * Size + Size / 2, cPos.Y * Size + Size / 2);
		} else {
			this.Biome = Biome.FOREST;
		}

		this.Position = new Vector2(cPos.X * Size * TilePixelSize, cPos.Y * Size * TilePixelSize);

		BackgroundLayer = new TileMapLayer { TileSet = tileSet, Name = "Background", ZIndex = -1 };
		GroundLayer = new TileMapLayer { TileSet = tileSet, Name = "Ground" };
		
		AddChild(BackgroundLayer);
		AddChild(GroundLayer);

		Generate(generator);
	}
	

public void InitializeFromCache(Vector2I cPos, TileSet tileSet, Tile[,] cachedGrid, WorldGenerator generator) {
	this.ChunkPos = cPos;
	this.Grid = (Tile[,])cachedGrid.Clone(); 
	
	this.Position = new Vector2(cPos.X * Size * TilePixelSize, cPos.Y * Size * TilePixelSize);

	BackgroundLayer = new TileMapLayer { TileSet = tileSet, Name = "Background", ZIndex = -1 };
	GroundLayer = new TileMapLayer { TileSet = tileSet, Name = "Ground" };
	
	AddChild(BackgroundLayer);
	AddChild(GroundLayer);


	for (int x = 0; x < Size; x++) {
		for (int y = 0; y < Size; y++) {
			int worldX = ChunkPos.X * Size + x;
			int worldY = ChunkPos.Y * Size + y;
			int surfaceY = generator.GetSurfaceHeight(worldX);
			
			Tile tile = Grid[x, y];
			DrawTile(x, y, tile, worldX, worldY, surfaceY, generator);
			if (tile.Type == TileType.LIQUID) {
				ActiveTiles.Add(new Vector2I(x, y));
			}
			if (tile.TileID == EarthID) {
				if (generator.GenerateTile(worldX, worldY - 1, surfaceY).Type == TileType.AIR) {
					OvergrowableTiles.Add(new Vector2I(x, y));
				}
			}
		}
	}
}
	
	

	private void Generate(WorldGenerator generator) {
		if (generator == null) return;

		for (int x = 0; x < Size; x++) {
			int worldX = ChunkPos.X * Size + x;
			int surfaceY = generator.GetSurfaceHeight(worldX);

			for (int y = 0; y < Size; y++) {
				int worldY = ChunkPos.Y * Size + y;
				Tile genTile = generator.GenerateTile(worldX, worldY, surfaceY);
				Grid[x, y] = genTile;
				if (genTile.TileID == EarthID) {
					if (generator.GenerateTile(worldX, worldY - 1, surfaceY).Type == TileType.AIR) {
						OvergrowableTiles.Add(new Vector2I(x, y));
					}
				}
				if (genTile.Type == TileType.LIQUID) {
					ActiveTiles.Add(new Vector2I(x, y));
				}

				DrawTile(x, y, genTile, worldX, worldY, surfaceY, generator);
			}
		}
	}

public void DrawTile(int x, int y, Tile tile, int worldX, int worldY, int surfaceY, WorldGenerator generator) {
	Vector2I localPos = new Vector2I(x, y);
	TileResource res = TileRegistry.Get(tile.TileID);
	
	if (res == null) {
		GroundLayer.SetCell(localPos, -1);
		return;
	}

	// Vordergrund
	if (res.Type != TileType.AIR) {
		// Wenn kein Terrain (-1) definiert ist, nutze die statischen Atlas-Koordinaten
		if (res.Terrain == -1) {
			GroundLayer.SetCell(localPos, 1, res.StaticAtlasCoords);
		} else {
			// Wenn Terrain definiert ist, nutze Autotiling
			var cells = new Godot.Collections.Array<Vector2I> { localPos };
			GroundLayer.SetCellsTerrainConnect(cells, res.TerrainSet, res.Terrain);
		}
	} else {
		GroundLayer.SetCell(localPos, -1);
	}

	// Hintergrund-Logik
	if (worldY > surfaceY) {
		Vector2I bgCoords = res.BackgroundCoords;
		// Wenn das aktuelle Tile keine eigenen Hintergrund-Koordinaten hat, 
		// nimm die von ID 1 (Erde) als Standard
		if (bgCoords == new Vector2I(-1, -1)) {
			bgCoords = new Vector2I(2, 2); // Dein alter Standard für Erde-BG
		}
		BackgroundLayer.SetCell(localPos, 1, bgCoords);
	} else {
		BackgroundLayer.SetCell(localPos, -1);
	}
}
	
	
	public void Tick(PhysicsHandler handler) {
	if (ActiveTiles.Count == 0) return;
	int currentUpdates = 0;
	var toProcess = new List<Vector2I>(ActiveTiles);
	ActiveTiles.Clear();

	foreach (var localPos in toProcess) {
		if(currentUpdates >= MaxUpdates) {
			ActiveTiles.Add(localPos);
			break;
		}
		Tile tile = Grid[localPos.X, localPos.Y];
		if (tile.Type == TileType.LIQUID) {
			handler.SimulateLiquid(this, localPos, tile);
			currentUpdates++;
		}
	}
}
	
}
