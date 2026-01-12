using Godot;
using System;
using System.Collections.Generic;

public static class TileAtlasData 
{		
	
	public static Dictionary<int, Vector2I> TilesetData = new Dictionary<int, Vector2I>() {
	
		{0, new Vector2I(-1, -1) },
		{1, new Vector2I(1, 0) }, // Erde
		{2, new Vector2I(0, 0) }, // Gras
		{3, new Vector2I(15, 0) }, // Wasser
		{4, new Vector2I(6, 0) }, // Magma
		{5, new Vector2I(2, 0) },  // Dunkle Erde
		{6, new Vector2I(3, 0) },  // Stein
		{7, new Vector2I(4, 0) },  // Dunkler Stein
		{8, new Vector2I(7, 0) }, // Kohle
		{9, new Vector2I(8, 0) }, // Eisen
		{10, new Vector2I(9, 0) }, // Gold
		{11, new Vector2I(10, 0) }, // Diamant
		{12, new Vector2I(12, 0) }, // Bedrock
		{13, new Vector2I(12, 0) },
		{14, new Vector2I(13, 0) },
	};
		
	public static Dictionary<int, Vector2I> BackgroundTileData = new Dictionary<int, Vector2I>() {
		{0, new Vector2I(-1, -1) },
		{1, new Vector2I(2, 2) }, // Erde

	};
	
}
