using Godot;
using System.Collections.Generic;
using static GlobalConstants;
public static class TileRegistry {
	private static readonly Dictionary<int, TileResource> _registry = new();

	public static void LoadAll() {
		_registry.Clear();
		string path = "res://Tiles/"; 
		// TODO: pfad anpassen, alle .tres dateien erstellen
		using var dir = DirAccess.Open(path);
		if (dir != null) {
			dir.ListDirBegin();
			string fileName = dir.GetNext();
			while (fileName != "") {
				if (fileName.EndsWith(".tres")) {
					var res = GD.Load<TileResource>(path + fileName);
					if (res != null) {
						_registry[res.TileID] = res;
					}
				}
				fileName = dir.GetNext();
			}
		}
	}

	public static TileResource Get(int id) {
		// wenn alle .tres dateien da sind bleibt nur noch das hier übrig
		if (_registry.TryGetValue(id, out var res)) {
			return res;
		}
		
		// das hier verschwindet später alles wieder 
		if (TileAtlasData.TilesetData.TryGetValue(id, out Vector2I coords)) {
			TileResource fallback = new TileResource();
			fallback.TileID = id;
			fallback.StaticAtlasCoords = coords;
			
			if (id == 0) fallback.Type = TileType.AIR;
			else if (id == 3 || id == 4) fallback.Type = TileType.LIQUID;
			else fallback.Type = TileType.STATIC;

			// Viskosität für Magma
			fallback.Viscosity = (id == 4) ? 0.2f : 1.0f;

			if (TileAtlasData.BackgroundTileData.TryGetValue(id, out Vector2I bgCoords)) {
				fallback.BackgroundCoords = bgCoords;
			}

			return fallback;
		}
		GD.PrintErr($"Warnung: Keine Resource für TileID {id} gefunden!");
		return null;
	}
}
