using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Facepunch;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;

namespace ConVar;

[Factory("copypaste")]
public class CopyPaste : ConsoleSystem
{
	private class EntityWrapper
	{
		public BaseEntity Entity;

		public ProtoBuf.Entity Protobuf;

		public Vector3 Position;

		public Quaternion Rotation;

		public bool HasParent;
	}

	private class PasteOptions
	{
		public const string Argument_NPCs = "--npcs";

		public const string Argument_Resources = "--resources";

		public const string Argument_Vehicles = "--vehicles";

		public const string Argument_Deployables = "--deployables";

		public const string Argument_FoundationsOnly = "--foundations-only";

		public const string Argument_BuildingBlocksOnly = "--building-only";

		public const string Argument_SnapToTerrain = "--autosnap-terrain";

		public const string Argument_PastePlayers = "--players";

		public bool Resources;

		public bool NPCs;

		public bool Vehicles;

		public bool Deployables;

		public bool FoundationsOnly;

		public bool BuildingBlocksOnly;

		public bool SnapToTerrain;

		public bool Players;

		public Vector3 HeightOffset;

		public PasteOptions(Arg arg)
		{
			Resources = arg.HasArg("--resources", remove: true);
			NPCs = arg.HasArg("--npcs", remove: true);
			Vehicles = arg.HasArg("--vehicles", remove: true);
			Deployables = arg.HasArg("--deployables", remove: true);
			FoundationsOnly = arg.HasArg("--foundations-only", remove: true);
			BuildingBlocksOnly = arg.HasArg("--building-only", remove: true);
			SnapToTerrain = arg.HasArg("--autosnap-terrain", remove: true);
			Players = arg.HasArg("--players", remove: true);
		}
	}

	private const string COPY_PASTE_FOLDER = "copypaste";

	private const string EXTENSION = "data";

	private static Dictionary<string, CopyPasteEntityInfo> cachedPastes = new Dictionary<string, CopyPasteEntityInfo>();

	private static CopyPasteEntityInfo clipboard;

	private static CopyPasteHistoryManager playerHistory = new CopyPasteHistoryManager();

	private static string GetPath(string name)
	{
		return Server.GetServerFolder("copypaste") + "/" + name + ".data";
	}

	private static void SaveClipboardInFile(string saveName)
	{
		if (clipboard == null)
		{
			UnityEngine.Debug.LogWarning("Clipboard empty");
			return;
		}
		string path = GetPath(saveName);
		File.WriteAllBytes(path, clipboard.ToProtoBytes());
		UnityEngine.Debug.Log($"Saved {clipboard.entities.Count} entities to {path}");
	}

	private static CopyPasteEntityInfo LoadFileInClipboard(string saveName)
	{
		string path = GetPath(saveName);
		if (!File.Exists(path))
		{
			UnityEngine.Debug.LogWarning("Missing file");
			return null;
		}
		CopyPasteEntityInfo copyPasteEntityInfo = CopyPasteEntityInfo.Deserialize(File.ReadAllBytes(path));
		cachedPastes[saveName] = copyPasteEntityInfo;
		UnityEngine.Debug.Log($"Loaded {copyPasteEntityInfo.entities.Count} entities from {path}");
		return copyPasteEntityInfo;
	}

	private static void CopyEntities(List<BaseEntity> entities, Vector3 originPos, Quaternion originRot)
	{
		OrderEntitiesForSave(entities);
		CopyPasteEntityInfo obj = Facepunch.Pool.Get<CopyPasteEntityInfo>();
		obj.entities = Facepunch.Pool.GetList<ProtoBuf.Entity>();
		Transform transform = new GameObject("Align").transform;
		transform.position = originPos;
		transform.rotation = originRot;
		foreach (BaseEntity entity in entities)
		{
			if (!entity.isClient && entity.enableSaving)
			{
				BaseEntity baseEntity = entity.parentEntity.Get(serverside: true);
				if (baseEntity != null && (!entities.Contains(baseEntity) || !baseEntity.enableSaving))
				{
					UnityEngine.Debug.LogWarning("Skipping " + entity.ShortPrefabName + " as it is parented to an entity not included in the copy (it would become orphaned)");
				}
				else
				{
					SaveEntity(entity, obj, baseEntity, transform);
				}
			}
		}
		UnityEngine.Object.Destroy(transform.gameObject);
		clipboard = obj.Copy();
		Facepunch.Pool.Free(ref obj);
	}

	private static List<BaseEntity> PasteEntities(CopyPasteEntityInfo toLoad, Vector3 originPoint, Quaternion rotation, PasteOptions options)
	{
		toLoad = toLoad.Copy();
		Transform transform = new GameObject("Align").transform;
		transform.position = originPoint;
		transform.rotation = rotation;
		List<EntityWrapper> list = new List<EntityWrapper>();
		Dictionary<ulong, ulong> remapping = new Dictionary<ulong, ulong>();
		Dictionary<uint, uint> dictionary = new Dictionary<uint, uint>();
		foreach (ProtoBuf.Entity entity in toLoad.entities)
		{
			entity.InspectUids(UpdateWithNewUid);
			EntityWrapper item = new EntityWrapper
			{
				Protobuf = entity,
				HasParent = (entity.parent != null && entity.parent.uid != default(NetworkableId))
			};
			list.Add(item);
			if (entity.decayEntity != null)
			{
				if (!dictionary.TryGetValue(entity.decayEntity.buildingID, out var value))
				{
					value = BuildingManager.server.NewBuildingID();
					dictionary.Add(entity.decayEntity.buildingID, value);
				}
				entity.decayEntity.buildingID = value;
			}
		}
		foreach (EntityWrapper item2 in list)
		{
			item2.Position = item2.Protobuf.baseEntity.pos;
			item2.Rotation = Quaternion.Euler(item2.Protobuf.baseEntity.rot);
			if (!item2.HasParent)
			{
				item2.Protobuf.baseEntity.pos = transform.TransformPoint(item2.Protobuf.baseEntity.pos);
				item2.Protobuf.baseEntity.rot = (transform.rotation * Quaternion.Euler(item2.Protobuf.baseEntity.rot)).eulerAngles;
			}
			if (CanPrefabBePasted(item2.Protobuf.baseNetworkable.prefabID, options))
			{
				item2.Entity = GameManager.server.CreateEntity(StringPool.Get(item2.Protobuf.baseNetworkable.prefabID), item2.Protobuf.baseEntity.pos, Quaternion.Euler(item2.Protobuf.baseEntity.rot));
				if (item2.Protobuf.basePlayer != null && item2.Protobuf.basePlayer.userid > 10000000)
				{
					ulong userid = 10000000uL + (ulong)UnityEngine.Random.Range(1, int.MaxValue);
					item2.Protobuf.basePlayer.userid = userid;
				}
				item2.Entity.InitLoad(item2.Protobuf.baseNetworkable.uid);
				item2.Entity.PreServerLoad();
			}
		}
		list.RemoveAll((EntityWrapper x) => x.Entity == null);
		UnityEngine.Object.Destroy(transform.gameObject);
		for (int i = 0; i < list.Count; i++)
		{
			EntityWrapper entityWrapper = list[i];
			BaseNetworkable.LoadInfo info = default(BaseNetworkable.LoadInfo);
			info.fromDisk = true;
			info.fromCopy = true;
			info.msg = entityWrapper.Protobuf;
			try
			{
				entityWrapper.Entity.Spawn();
				bool flag = false;
				if (!flag && entityWrapper.Protobuf.parent != null && entityWrapper.Protobuf.parent.uid != default(NetworkableId))
				{
					BaseEntity baseEntity = BaseNetworkable.serverEntities.Find(entityWrapper.Protobuf.parent.uid) as BaseEntity;
					if (baseEntity == null || baseEntity.net == null)
					{
						flag = true;
					}
				}
				if (flag)
				{
					entityWrapper.Entity.Kill();
					list.RemoveAt(i);
					i--;
				}
				else
				{
					entityWrapper.Entity.Load(info);
				}
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
				entityWrapper.Entity.Kill();
			}
		}
		float num = float.MaxValue;
		float num2 = float.MinValue;
		foreach (EntityWrapper item3 in list)
		{
			Vector3 position = item3.Entity.transform.position;
			if ((item3.Entity.parentEntity.Get(serverside: true) == null && item3.Entity.ShortPrefabName == "foundation") || item3.Entity.ShortPrefabName == "foundation.triangle")
			{
				float num3 = TerrainMeta.HeightMap.GetHeight(position);
				if (UnityEngine.Physics.Raycast(new Vector3(position.x, num3, position.z) + new Vector3(0f, 100f, 0f), Vector3.down, out var hitInfo, 100f, 8454144))
				{
					num3 = hitInfo.point.y;
				}
				if (position.y > num3)
				{
					num = Mathf.Min(num, position.y - num3);
				}
				if (num3 > position.y)
				{
					num2 = Mathf.Max(num2, num3 - position.y);
				}
			}
		}
		if (num == float.MaxValue && num2 == float.MinValue)
		{
			num2 = 0f;
			num = 0f;
		}
		Vector3 vector = new Vector3(0f, (num < num2 || num2 == float.MinValue) ? (num * -1f) : num2, 0f);
		vector += options.HeightOffset;
		if (vector != Vector3.zero)
		{
			foreach (EntityWrapper item4 in list)
			{
				if (item4.Entity.parentEntity.Get(serverside: true) == null)
				{
					item4.Entity.transform.position += vector;
				}
			}
		}
		foreach (EntityWrapper item5 in list)
		{
			item5.Entity.PostServerLoad();
			item5.Entity.UpdateNetworkGroup();
		}
		foreach (EntityWrapper item6 in list)
		{
			item6.Entity.RefreshEntityLinks();
		}
		UnityEngine.Debug.Log($"Pasted {list.Count} entities");
		return (from x in list
			select x.Entity into x
			where x != null
			select x).ToList();
		void UpdateWithNewUid(UidType type, ref ulong prevUid)
		{
			if (type == UidType.Clear)
			{
				prevUid = 0uL;
			}
			else if (prevUid != 0L)
			{
				if (!remapping.TryGetValue(prevUid, out var value2))
				{
					value2 = Network.Net.sv.TakeUID();
					remapping.Add(prevUid, value2);
				}
				prevUid = value2;
			}
		}
	}

	private static void SaveEntity(BaseEntity baseEntity, CopyPasteEntityInfo toSave, BaseEntity parent, Transform alignObject)
	{
		BaseNetworkable.SaveInfo info = default(BaseNetworkable.SaveInfo);
		info.forDisk = true;
		info.msg = Facepunch.Pool.Get<ProtoBuf.Entity>();
		baseEntity.Save(info);
		if (parent == null)
		{
			info.msg.baseEntity.pos = alignObject.InverseTransformPoint(info.msg.baseEntity.pos);
			_ = alignObject.rotation * baseEntity.transform.rotation;
			info.msg.baseEntity.rot = (Quaternion.Inverse(alignObject.transform.rotation) * baseEntity.transform.rotation).eulerAngles;
		}
		UnityEngine.Debug.Log($"Saving {baseEntity.ShortPrefabName} at {info.msg.baseEntity.pos} {info.msg.baseEntity.rot}");
		toSave.entities.Add(info.msg);
	}

	private static void GetEntitiesLookingAt(Vector3 originPoint, Vector3 direction, List<BaseEntity> entityList)
	{
		entityList.Clear();
		BuildingBlock buildingBlock = GamePhysics.RaycastEntity(GamePhysics.Realm.Server, new Ray(originPoint, direction), 100f, 2097408) as BuildingBlock;
		if (!(buildingBlock == null))
		{
			ListHashSet<DecayEntity> listHashSet = buildingBlock.GetBuilding()?.decayEntities;
			if (listHashSet != null)
			{
				entityList.AddRange(listHashSet);
			}
		}
	}

	private static void GetEntitiesInRadius(Vector3 originPoint, float radius, List<BaseEntity> entityList)
	{
		if (radius <= 0f)
		{
			UnityEngine.Debug.Log("Radius must be > 0");
			return;
		}
		List<BaseEntity> list = new List<BaseEntity>();
		global::Vis.Entities(originPoint, radius, list);
	}

	private static void GetEntitiesInBounds(Bounds bounds, List<BaseEntity> entityList)
	{
		global::Vis.Entities(new OBB(bounds), entityList);
	}

	private static bool CanPrefabBePasted(uint prefabId, PasteOptions options)
	{
		GameObject gameObject = GameManager.server.FindPrefab(prefabId);
		if (gameObject == null)
		{
			return false;
		}
		BaseEntity component = gameObject.GetComponent<BaseEntity>();
		if (component == null)
		{
			return false;
		}
		UnityEngine.Debug.Log("Checking prefab '" + component.ShortPrefabName + "'");
		if (options.FoundationsOnly && component.ShortPrefabName != "foundation" && component.ShortPrefabName != "foundation.triangle")
		{
			return false;
		}
		if (options.BuildingBlocksOnly && !(component is BuildingBlock))
		{
			return false;
		}
		if (component is DecayEntity && !(component is BuildingBlock) && !options.Deployables)
		{
			return false;
		}
		if (component is BasePlayer { IsNpc: false } && !options.Players)
		{
			return false;
		}
		if (component is PointEntity || component is RelationshipManager)
		{
			return false;
		}
		if ((component is ResourceEntity || component is BushEntity) && !options.Resources)
		{
			return false;
		}
		if ((component is BaseNpc || component is BaseRidableAnimal) && !options.NPCs)
		{
			return false;
		}
		if (component is BaseVehicle && !(component is BaseRidableAnimal) && !options.Vehicles)
		{
			return false;
		}
		return true;
	}

	private static void OrderEntitiesForSave(List<BaseEntity> entities)
	{
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		obj.AddRange(entities);
		entities.Clear();
		HashSet<BaseEntity> hash = new HashSet<BaseEntity>();
		foreach (BaseEntity item in obj.OrderBy((BaseEntity x) => x.net.ID.Value))
		{
			AddRecursive(item);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		void AddRecursive(BaseEntity current)
		{
			if (hash.Add(current))
			{
				entities.Add(current);
				if (current.children != null)
				{
					foreach (BaseEntity child in current.children)
					{
						AddRecursive(child);
					}
				}
			}
		}
	}

	private static CopyPasteEntityInfo GetOrLoadPaste(Arg args, string name)
	{
		if (name == "clipboard")
		{
			if (clipboard == null)
			{
				args.ReplyWith("Clipboard empty");
				return null;
			}
			return clipboard;
		}
		if (cachedPastes.TryGetValue(name, out var value))
		{
			return value;
		}
		value = LoadFileInClipboard(name);
		if (value != null)
		{
			return value;
		}
		args.ReplyWith("No save named '" + name + "' found");
		return null;
	}

	[ServerVar(Name = "copybox_sv")]
	public static void copybox_sv(Arg args)
	{
		if (!args.HasArgs(3))
		{
			args.ReplyWith("Missing args: copybox_sv <center> <size> <rotation>");
			return;
		}
		Vector3 vector = args.GetVector3(0);
		Vector3 vector2 = args.GetVector3(1);
		Quaternion originRot = Quaternion.Euler(args.GetVector3(2));
		Bounds bounds = new Bounds(vector, vector2);
		List<BaseEntity> obj = Facepunch.Pool.GetList<BaseEntity>();
		GetEntitiesInBounds(bounds, obj);
		CopyEntities(obj, vector, originRot);
		Facepunch.Pool.FreeList(ref obj);
	}

	[ServerVar]
	public static void paste_sv(Arg args)
	{
		if (!args.HasArgs(3))
		{
			args.ReplyWith("Missing args: paste_sv <name> <origin> <rotation>");
			return;
		}
		ulong steamId = args.Player()?.userID ?? ((BasePlayer.EncryptedValue<ulong>)0uL);
		string @string = args.GetString(0);
		Vector3 vector = args.GetVector3(1);
		Quaternion rotation = Quaternion.Euler(args.GetVector3(2));
		Vector3 vector2 = args.GetVector3(3);
		PasteOptions options = new PasteOptions(args)
		{
			HeightOffset = vector2
		};
		CopyPasteEntityInfo orLoadPaste = GetOrLoadPaste(args, @string);
		List<BaseEntity> entities;
		try
		{
			Rust.Application.isLoadingSave = true;
			entities = PasteEntities(orLoadPaste, vector, rotation, options);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
			return;
		}
		finally
		{
			Rust.Application.isLoadingSave = false;
		}
		playerHistory.AddToHistory(steamId, entities);
	}

	[ServerVar]
	public static void undopaste_sv(Arg args)
	{
		ulong steamId = args.Player()?.userID ?? ((BasePlayer.EncryptedValue<ulong>)0uL);
		PasteResult pasteResult = playerHistory.Undo(steamId);
		if (pasteResult == null)
		{
			args.ReplyWith("History empty");
			return;
		}
		foreach (BaseEntity entity in pasteResult.Entities)
		{
			entity.Kill();
		}
	}

	[ServerVar]
	public static void copyradius_sv(Arg args)
	{
		Vector3 vector = args.GetVector3(0);
		float @float = args.GetFloat(1);
		Quaternion originRot = Quaternion.Euler(args.GetVector3(2));
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		GetEntitiesInRadius(vector, @float, obj);
		CopyEntities(obj, vector, originRot);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	[ServerVar]
	public static void copybuilding_sv(Arg args)
	{
		Vector3 vector = args.GetVector3(0);
		Vector3 vector2 = args.GetVector3(1);
		Quaternion originRot = Quaternion.Euler(args.GetVector3(2));
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		GetEntitiesLookingAt(vector, vector2, obj);
		CopyEntities(obj, vector, originRot);
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	[ServerVar]
	public static void printselection_sv(Arg args)
	{
		List<BaseEntity> obj = Facepunch.Pool.GetList<BaseEntity>();
		Vector3 vector = args.GetVector3(0);
		Vector3 vector2 = args.GetVector3(1);
		args.GetVector3(2);
		GetEntitiesInBounds(new Bounds(vector, vector2), obj);
		if (obj.Count == 0)
		{
			UnityEngine.Debug.Log("Empty");
		}
		else
		{
			foreach (BaseEntity item in obj)
			{
				if (!item.isClient)
				{
					UnityEngine.Debug.Log(item.name);
				}
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	[ServerVar]
	public static void killbox_sv(Arg args)
	{
		Vector3 vector = args.GetVector3(0);
		Vector3 vector2 = args.GetVector3(1);
		PasteOptions options = new PasteOptions(args);
		Bounds bounds = new Bounds(vector, vector2);
		List<BaseEntity> obj = Facepunch.Pool.Get<List<BaseEntity>>();
		GetEntitiesInBounds(bounds, obj);
		foreach (BaseEntity item in obj)
		{
			if (!item.isClient && !CanPrefabBePasted(item.prefabID, options))
			{
				item.Kill();
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	[ServerVar(Name = "savepaste", Help = "<name>")]
	public static void save_sv(Arg args)
	{
		string @string = args.GetString(0);
		if (!string.IsNullOrEmpty(@string))
		{
			SaveClipboardInFile(@string);
			cachedPastes[@string] = clipboard;
		}
	}

	[ServerVar(Name = "loadpaste", Help = "<name>")]
	public static void load_sv(Arg args)
	{
		string @string = args.GetString(0);
		if (!string.IsNullOrEmpty(@string))
		{
			clipboard = LoadFileInClipboard(@string);
			cachedPastes[@string] = clipboard;
		}
	}

	[ServerVar(Name = "downloadpaste", Help = "<url> <name>")]
	public static void downloadpaste(Arg args)
	{
		string url = args.GetString(0);
		string name = args.GetString(1);
		if (string.IsNullOrEmpty(name))
		{
			name = Path.GetFileNameWithoutExtension(url);
		}
		string path = GetPath(name);
		if (File.Exists(path))
		{
			if (string.IsNullOrEmpty(name))
			{
				args.ReplyWith("Paste '" + name + "' already exists on disk! Provide a differnet filename with `downloadpaste <url> <name>");
			}
			else
			{
				args.ReplyWith("Paste '" + name + "' already exists on disk! Please use a different name");
			}
			return;
		}
		using WebClient webClient = new WebClient();
		BasePlayer player = args.Player();
		Stopwatch sw = Stopwatch.StartNew();
		webClient.DownloadFileTaskAsync(new Uri(url), path).ContinueWith(delegate(Task task)
		{
			if (task.IsFaulted)
			{
				player.ConsoleMessage("Failed to download '" + url + "'");
			}
			else
			{
				player.ConsoleMessage($"Downloaded paste '{name}' from {url} in {Math.Round(sw.Elapsed.TotalSeconds, 1)}s");
			}
		});
	}

	[ServerVar(Help = "<old> <new>")]
	public static void renamepaste(Arg args)
	{
		if (!args.HasArgs(2))
		{
			args.ReplyWith("Missing args: renamepaste <old> <new>");
			return;
		}
		string @string = args.GetString(0);
		string string2 = args.GetString(1);
		string path = GetPath(@string);
		string path2 = GetPath(string2);
		if (!File.Exists(path))
		{
			args.ReplyWith("Paste '" + @string + "' does not exist");
			return;
		}
		if (File.Exists(path2))
		{
			args.ReplyWith("Paste '" + string2 + "' already exists, please use a different name");
			return;
		}
		File.Move(path, path2);
		args.ReplyWith("Renamed paste from '" + @string + "' to '" + string2 + "'");
		cachedPastes.Remove(@string);
	}

	[ServerVar(Help = "<name>")]
	public static void deletepaste(Arg args)
	{
		if (!args.HasArgs())
		{
			args.ReplyWith("Missing args: deletepaste <name>");
			return;
		}
		string @string = args.GetString(0);
		string path = GetPath(@string);
		if (!File.Exists(path))
		{
			args.ReplyWith("Paste '" + @string + "' does not exist");
			return;
		}
		File.Delete(path);
		args.ReplyWith("Deleted paste '" + @string + "'");
		cachedPastes.Remove(@string);
	}

	private static Quaternion GetPlayerRotation(BasePlayer ply)
	{
		Vector3 forward = ply.eyes.BodyForward();
		forward.y = 0f;
		return Quaternion.LookRotation(forward, Vector3.up);
	}
}
