using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Facepunch.Rust;
using Network;
using UnityEngine;
using UnityEngine.SceneManagement;

[Factory("global")]
public class DiagnosticsConSys : ConsoleSystem
{
	private static void DumpAnimators(string targetFolder)
	{
		Animator[] array = UnityEngine.Object.FindObjectsOfType<Animator>();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("All animators");
		stringBuilder.AppendLine();
		Animator[] array2 = array;
		foreach (Animator animator in array2)
		{
			stringBuilder.AppendFormat("{1}\t{0}", animator.transform.GetRecursiveName(), animator.enabled);
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Animators.List.txt", stringBuilder.ToString());
		StringBuilder stringBuilder2 = new StringBuilder();
		stringBuilder2.AppendLine("All animators - grouped by object name");
		stringBuilder2.AppendLine();
		foreach (IGrouping<string, Animator> item in from x in array
			group x by x.transform.GetRecursiveName() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder2.AppendFormat("{1:N0}\t{0}", item.First().transform.GetRecursiveName(), item.Count());
			stringBuilder2.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Animators.Counts.txt", stringBuilder2.ToString());
		StringBuilder stringBuilder3 = new StringBuilder();
		stringBuilder3.AppendLine("All animators - grouped by enabled/disabled");
		stringBuilder3.AppendLine();
		foreach (IGrouping<string, Animator> item2 in from x in array
			group x by x.transform.GetRecursiveName(x.enabled ? "" : " (DISABLED)") into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder3.AppendFormat("{1:N0}\t{0}", item2.First().transform.GetRecursiveName(item2.First().enabled ? "" : " (DISABLED)"), item2.Count());
			stringBuilder3.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Animators.Counts.Enabled.txt", stringBuilder3.ToString());
	}

	[ServerVar]
	[ClientVar]
	public static void dump(Arg args)
	{
		if (Directory.Exists("diagnostics"))
		{
			Directory.CreateDirectory("diagnostics");
		}
		int num = 1;
		while (Directory.Exists("diagnostics/" + num))
		{
			num++;
		}
		Directory.CreateDirectory("diagnostics/" + num);
		string targetFolder = "diagnostics/" + num + "/";
		DumpLODGroups(targetFolder);
		DumpSystemInformation(targetFolder);
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			DumpSceneGameObjects(targetFolder, i);
		}
		DumpAllGameObjects(targetFolder);
		DumpObjects(targetFolder);
		DumpEntities(targetFolder);
		DumpNetwork(targetFolder);
		DumpPhysics(targetFolder);
		DumpAnimators(targetFolder);
		DumpWarmup(targetFolder);
	}

	private static void DumpSystemInformation(string targetFolder)
	{
		WriteTextToFile(targetFolder + "System.Info.txt", SystemInfoGeneralText.currentInfo);
	}

	private static void WriteTextToFile(string file, string text)
	{
		File.WriteAllText(file, text);
	}

	private static void DumpEntities(string targetFolder)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("All entities");
		stringBuilder.AppendLine();
		foreach (BaseNetworkable serverEntity in BaseNetworkable.serverEntities)
		{
			string prefabName = serverEntity.PrefabName;
			NetworkableId obj = serverEntity.net?.ID ?? default(NetworkableId);
			stringBuilder.AppendFormat("{1}\t{0}", prefabName, obj.Value);
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Entity.SV.List.txt", stringBuilder.ToString());
		StringBuilder stringBuilder2 = new StringBuilder();
		stringBuilder2.AppendLine("All entities");
		stringBuilder2.AppendLine();
		foreach (IGrouping<uint, BaseNetworkable> item in from x in BaseNetworkable.serverEntities
			group x by x.prefabID into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder2.AppendFormat("{1:N0}\t{0}", item.First().PrefabName, item.Count());
			stringBuilder2.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Entity.SV.Counts.txt", stringBuilder2.ToString());
		StringBuilder stringBuilder3 = new StringBuilder();
		stringBuilder3.AppendLine("Saved entities");
		stringBuilder3.AppendLine();
		foreach (IGrouping<uint, BaseEntity> item2 in from x in BaseEntity.saveList
			group x by x.prefabID into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder3.AppendFormat("{1:N0}\t{0}", item2.First().PrefabName, item2.Count());
			stringBuilder3.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Entity.SV.Savelist.Counts.txt", stringBuilder3.ToString());
	}

	private static void DumpLODGroups(string targetFolder)
	{
		DumpLODGroupTotals(targetFolder);
	}

	private static void DumpLODGroupTotals(string targetFolder)
	{
		LODGroup[] source = UnityEngine.Object.FindObjectsOfType<LODGroup>();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("LODGroups");
		stringBuilder.AppendLine();
		foreach (IGrouping<string, LODGroup> item in from x in source
			group x by x.transform.GetRecursiveName() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder.AppendFormat("{1:N0}\t{0}", item.Key, item.Count());
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "LODGroups.Objects.txt", stringBuilder.ToString());
	}

	private static void DumpNetwork(string targetFolder)
	{
		if (!Net.sv.IsConnected())
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Server Network Statistics");
		stringBuilder.AppendLine();
		stringBuilder.Append(Net.sv.GetDebug(null).Replace("\n", "\r\n"));
		stringBuilder.AppendLine();
		foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
		{
			stringBuilder.AppendLine("Name: " + activePlayer.displayName);
			stringBuilder.AppendLine("SteamID: " + activePlayer.userID.Get());
			stringBuilder.Append((activePlayer.net == null) ? "INVALID - NET IS NULL" : Net.sv.GetDebug(activePlayer.net.connection).Replace("\n", "\r\n"));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine();
			stringBuilder.AppendLine();
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "Network.Server.txt", stringBuilder.ToString());
	}

	private static void DumpObjects(string targetFolder)
	{
		UnityEngine.Object[] array = UnityEngine.Object.FindObjectsOfType<UnityEngine.Object>();
		UnityEngine.Object[] array2 = UnityEngine.Object.FindObjectsOfType<UnityEngine.Object>(includeInactive: true);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("All active UnityEngine.Object, ordered by count");
		stringBuilder.AppendLine($"Total: {array.Length}");
		stringBuilder.AppendLine();
		foreach (IGrouping<Type, UnityEngine.Object> item in from x in array
			group x by x.GetType() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder.AppendFormat("{1:N0}\t{0}", item.First().GetType().Name, item.Count());
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Object.Count.txt", stringBuilder.ToString());
		StringBuilder stringBuilder2 = new StringBuilder();
		stringBuilder2.AppendLine("All active + inactive UnityEngine.Object, ordered by count");
		stringBuilder2.AppendLine($"Total: {array2.Length}");
		stringBuilder2.AppendLine();
		foreach (IGrouping<Type, UnityEngine.Object> item2 in from x in array2
			group x by x.GetType() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder2.AppendFormat("{1:N0}\t{0}", item2.First().GetType().Name, item2.Count());
			stringBuilder2.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Object_All.Count.txt", stringBuilder2.ToString());
		StringBuilder stringBuilder3 = new StringBuilder();
		stringBuilder3.AppendLine("All active + inactive UnityEngine.Object, ordered by count");
		stringBuilder3.AppendLine($"Total: {array.Length}");
		stringBuilder3.AppendLine();
		foreach (IGrouping<Type, UnityEngine.Object> item3 in from x in array
			group x by x.GetType() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder3.AppendFormat("{1:N0}\t{0}", item3.First().GetType().Name, item3.Count());
			stringBuilder3.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Object.Count.txt", stringBuilder3.ToString());
		StringBuilder stringBuilder4 = new StringBuilder();
		stringBuilder4.AppendLine("All active UnityEngine.ScriptableObject, ordered by count");
		stringBuilder4.AppendLine();
		foreach (IGrouping<Type, UnityEngine.Object> item4 in from x in array
			where x is ScriptableObject
			group x by x.GetType() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder4.AppendFormat("{1:N0}\t{0}", item4.First().GetType().Name, item4.Count());
			stringBuilder4.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.ScriptableObject.Count.txt", stringBuilder4.ToString());
	}

	private static void DumpPhysics(string targetFolder)
	{
		DumpTotals(targetFolder);
		DumpColliders(targetFolder);
		DumpRigidBodies(targetFolder);
	}

	private static void DumpTotals(string targetFolder)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Physics Information");
		stringBuilder.AppendLine();
		stringBuilder.AppendFormat("Total Colliders:\t{0:N0}", UnityEngine.Object.FindObjectsOfType<Collider>().Count());
		stringBuilder.AppendLine();
		stringBuilder.AppendFormat("Active Colliders:\t{0:N0}", (from x in UnityEngine.Object.FindObjectsOfType<Collider>()
			where x.enabled
			select x).Count());
		stringBuilder.AppendLine();
		stringBuilder.AppendFormat("Total RigidBodys:\t{0:N0}", UnityEngine.Object.FindObjectsOfType<Rigidbody>().Count());
		stringBuilder.AppendLine();
		stringBuilder.AppendLine();
		stringBuilder.AppendFormat("Mesh Colliders:\t{0:N0}", UnityEngine.Object.FindObjectsOfType<MeshCollider>().Count());
		stringBuilder.AppendLine();
		stringBuilder.AppendFormat("Box Colliders:\t{0:N0}", UnityEngine.Object.FindObjectsOfType<BoxCollider>().Count());
		stringBuilder.AppendLine();
		stringBuilder.AppendFormat("Sphere Colliders:\t{0:N0}", UnityEngine.Object.FindObjectsOfType<SphereCollider>().Count());
		stringBuilder.AppendLine();
		stringBuilder.AppendFormat("Capsule Colliders:\t{0:N0}", UnityEngine.Object.FindObjectsOfType<CapsuleCollider>().Count());
		stringBuilder.AppendLine();
		WriteTextToFile(targetFolder + "Physics.txt", stringBuilder.ToString());
	}

	private static void DumpColliders(string targetFolder)
	{
		Collider[] source = UnityEngine.Object.FindObjectsOfType<Collider>();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Physics Colliders");
		stringBuilder.AppendLine();
		foreach (IGrouping<string, Collider> item in from x in source
			group x by x.transform.GetRecursiveName() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder.AppendFormat("{1:N0}\t{0} ({2:N0} triggers) ({3:N0} enabled)", item.Key, item.Count(), item.Count((Collider x) => x.isTrigger), item.Count((Collider x) => x.enabled));
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "Physics.Colliders.Objects.txt", stringBuilder.ToString());
	}

	private static void DumpRigidBodies(string targetFolder)
	{
		Rigidbody[] source = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("RigidBody");
		stringBuilder.AppendLine();
		StringBuilder stringBuilder2 = new StringBuilder();
		stringBuilder2.AppendLine("RigidBody");
		stringBuilder2.AppendLine();
		foreach (IGrouping<string, Rigidbody> item in from x in source
			group x by x.transform.GetRecursiveName() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder.AppendFormat("{1:N0}\t{0} ({2:N0} awake) ({3:N0} kinematic) ({4:N0} non-discrete)", item.Key, item.Count(), item.Count((Rigidbody x) => !x.IsSleeping()), item.Count((Rigidbody x) => x.isKinematic), item.Count((Rigidbody x) => x.collisionDetectionMode != CollisionDetectionMode.Discrete));
			stringBuilder.AppendLine();
			foreach (Rigidbody item2 in item)
			{
				stringBuilder2.AppendFormat("{0} -{1}{2}{3}", item.Key, item2.isKinematic ? " KIN" : "", item2.IsSleeping() ? " SLEEP" : "", item2.useGravity ? " GRAVITY" : "");
				stringBuilder2.AppendLine();
				stringBuilder2.AppendFormat("Mass: {0}\tVelocity: {1}\tsleepThreshold: {2}", item2.mass, item2.velocity, item2.sleepThreshold);
				stringBuilder2.AppendLine();
				stringBuilder2.AppendLine();
			}
		}
		WriteTextToFile(targetFolder + "Physics.RigidBody.Objects.txt", stringBuilder.ToString());
		WriteTextToFile(targetFolder + "Physics.RigidBody.All.txt", stringBuilder2.ToString());
	}

	private static string GetOutputDirectoryForScene(string targetFolder, int sceneIndex, Scene scene)
	{
		string arg = scene.name.Replace("\\", "_").Replace("/", "_").Replace(" ", "_");
		targetFolder = Path.Combine(targetFolder, "Scenes", $"{sceneIndex}_{arg}/");
		Directory.CreateDirectory(targetFolder);
		return targetFolder;
	}

	private static void DumpSceneGameObjects(string targetFolder, int sceneIndex)
	{
		Scene sceneAt = SceneManager.GetSceneAt(sceneIndex);
		Transform[] rootObjects = (from x in sceneAt.GetRootGameObjects()
			select x.transform).ToArray();
		targetFolder = GetOutputDirectoryForScene(targetFolder, sceneIndex, sceneAt);
		DumpGameObjects(targetFolder, rootObjects);
	}

	private static void DumpAllGameObjects(string targetFolder)
	{
		Transform[] rootObjects = TransformUtil.GetRootObjects();
		DumpGameObjects(targetFolder, rootObjects);
	}

	private static void DumpGameObjects(string targetFolder, Transform[] rootObjects)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("All active game objects");
		stringBuilder.AppendLine();
		Transform[] array = rootObjects;
		foreach (Transform tx in array)
		{
			DumpGameObjectRecursive(stringBuilder, tx, 0);
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "GameObject.Hierarchy.txt", stringBuilder.ToString());
		stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("All active game objects including components");
		stringBuilder.AppendLine();
		array = rootObjects;
		foreach (Transform tx2 in array)
		{
			DumpGameObjectRecursive(stringBuilder, tx2, 0, includeComponents: true);
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "GameObject.Hierarchy.Components.txt", stringBuilder.ToString());
		stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Root gameobjects, grouped by name, ordered by the total number of objects excluding children");
		stringBuilder.AppendLine();
		foreach (IGrouping<string, Transform> item in from x in rootObjects
			group x by x.name into x
			orderby x.Count() descending
			select x)
		{
			Transform transform = item.First();
			stringBuilder.AppendFormat("{1:N0}\t{0}", transform.name, item.Count());
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "GameObject.Count.txt", stringBuilder.ToString());
		stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("Root gameobjects, grouped by name, ordered by the total number of objects including children");
		stringBuilder.AppendLine();
		foreach (KeyValuePair<Transform, int> item2 in from x in rootObjects
			group x by x.name into x
			select new KeyValuePair<Transform, int>(x.First(), x.Sum((Transform y) => y.GetAllChildren().Count)) into x
			orderby x.Value descending
			select x)
		{
			stringBuilder.AppendFormat("{1:N0}\t{0}", item2.Key.name, item2.Value);
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "GameObject.Count.Children.txt", stringBuilder.ToString());
		Component[] source = (from x in rootObjects.SelectMany((Transform x) => x.GetComponentsInChildren<Component>(includeInactive: true))
			where x != null
			select x).ToArray();
		UnityEngine.Object[] array2 = source.Select((Component x) => x.gameObject).Distinct().ToArray()
			.OfType<UnityEngine.Object>()
			.Concat(source.OfType<UnityEngine.Object>())
			.ToArray();
		stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("All UnityEngine.Object active + inactive, ordered by count");
		stringBuilder.AppendLine($"Total: {array2.Length}");
		foreach (IGrouping<Type, UnityEngine.Object> item3 in from x in array2
			group x by x.GetType() into x
			orderby x.Count() descending
			select x)
		{
			stringBuilder.AppendFormat("{1:N0}\t{0}", item3.First().GetType().Name, item3.Count());
			stringBuilder.AppendLine();
		}
		WriteTextToFile(targetFolder + "UnityEngine.Object.Count.txt", stringBuilder.ToString());
	}

	private static void DumpGameObjectRecursive(StringBuilder str, Transform tx, int indent, bool includeComponents = false)
	{
		if (tx == null)
		{
			return;
		}
		str.Append(' ', indent);
		str.Append(tx.gameObject.activeSelf ? "+ " : "- ");
		str.Append(tx.name);
		str.Append(" [").Append(tx.GetComponents<Component>().Length - 1).Append(']');
		str.AppendLine();
		if (includeComponents)
		{
			Component[] components = tx.GetComponents<Component>();
			foreach (Component component in components)
			{
				if (!(component is Transform))
				{
					str.Append(' ', indent + 3);
					bool? flag = component.IsEnabled();
					if (!flag.HasValue)
					{
						str.Append("[~] ");
					}
					else if (flag == true)
					{
						str.Append("[✓] ");
					}
					else
					{
						str.Append("[ ] ");
					}
					str.Append((component == null) ? "NULL" : component.GetType().ToString());
					str.AppendLine();
				}
			}
		}
		for (int j = 0; j < tx.childCount; j++)
		{
			DumpGameObjectRecursive(str, tx.GetChild(j), indent + 4, includeComponents);
		}
	}

	private static void DumpWarmup(string targetFolder)
	{
		DumpWarmupTimings(targetFolder);
		DumpWorldSpawnTimings(targetFolder);
	}

	private static void DumpWarmupTimings(string targetFolder)
	{
		if (!FileSystem_Warmup.GetWarmupTimes().Any())
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("index,prefab,time");
		int num = 0;
		foreach (var warmupTime in FileSystem_Warmup.GetWarmupTimes())
		{
			object arg = num;
			var (arg2, timeSpan) = warmupTime;
			stringBuilder.AppendLine($"{arg},{arg2},{timeSpan.Ticks * EventRecord.TicksToNS}");
			num++;
		}
		WriteTextToFile(targetFolder + "Asset.Warmup.csv", stringBuilder.ToString());
	}

	private static void DumpWorldSpawnTimings(string targetFolder)
	{
		if (!World.GetSpawnTimings().Any())
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("index,prefab,time,category,position,rotation");
		int num = 0;
		foreach (World.SpawnTiming spawnTiming in World.GetSpawnTimings())
		{
			object[] obj = new object[6]
			{
				num,
				spawnTiming.prefab.Name,
				null,
				null,
				null,
				null
			};
			TimeSpan time = spawnTiming.time;
			obj[2] = time.Ticks * EventRecord.TicksToNS;
			obj[3] = spawnTiming.category;
			obj[4] = spawnTiming.position;
			obj[5] = spawnTiming.rotation;
			stringBuilder.AppendLine(string.Format("{0},{1},{2},{3},{4},{5}", obj));
			num++;
		}
		WriteTextToFile(targetFolder + "World.Spawn.csv", stringBuilder.ToString());
	}
}
