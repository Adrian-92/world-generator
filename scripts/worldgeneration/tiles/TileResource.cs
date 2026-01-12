using Godot;
using static GlobalConstants;

[GlobalClass]
public partial class TileResource : Resource 
{
	[ExportGroup("Physics & Logic")]
	
	/// <summary>
	/// Die eindeutige ID des Tiles. Muss mit der ID im PhysicsHandler übereinstimmen.
	/// </summary>
	[Export] public int TileID;

	/// <summary>
	/// Bestimmt das physikalische Verhalten (z.B. fest, flüssig oder Luft).
	/// </summary>
	[Export] public TileType Type;

	/// <summary>
	/// (1.0 = normal, 0.2 = zäh)
	/// </summary>
	[Export(PropertyHint.Range, "0.0, 1.0, 0.05")] 
	public float Viscosity = 1.0f;

	/// <summary>
	/// Für später, wenn man es abbauen will etc.
	/// </summary>
	[Export] public int MaxHealth;
	/// <summary>
	/// Für später wenn health system implementiert ist
	/// </summary>
	[Export] public int DamageOnContact;
	
	[ExportGroup("Visuals")]

	/// <summary>
	/// Terrain-Set aus dem TileSet-Editor.
	/// </summary>
	[Export] public int TerrainSet = 0;

	/// <summary>
	/// Die ID des spezifischen Terrains (z.B. 0 für Gras, 1 für Erde). 
	/// -1, um Autotiling zu deaktivieren.
	/// </summary>
	[Export] public int Terrain = -1;

	/// <summary>
	/// Die festen Koordinaten im Atlas, falls KEIN Autotiling (Terrain = -1) genutzt wird.
	/// </summary>
	[ExportGroup("Visuals (Legacy Fallback)")]
	[Export] public Vector2I StaticAtlasCoords = new Vector2I(-1, -1);
	[Export] public Vector2I BackgroundCoords = new Vector2I(-1, -1);
}
