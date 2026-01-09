using Godot;
using System;
using System.Collections.Generic;
using static GlobalConstants;

public class Chunk {
	public const int Size = 32;
	public int[,] Grid;
	public Vector2I ChunkPos;
	public HashSet<Vector2I> ActiveTiles = new HashSet<Vector2I>();
	public HashSet<Vector2I> OvergrowableTiles = new HashSet<Vector2I>();
	public bool IsDirty;
	public bool IsActive; 
	public Biome Biome;
	
	public Chunk(Vector2I pos, WorldGenerator gen) {
		ChunkPos = pos;
		Grid = new int[Size, Size];
		if (gen != null) {
			Biome = gen.GetBiomeAt(pos.X * Size + Size/2, pos.Y * Size + Size/2);
		} else {
			Biome = Biome.FOREST;
		}
	}
	
	public static int ToLocal(int worldCoord) {
		int res = worldCoord % Size;
		return res < 0 ? res + Size : res;
	}
}
