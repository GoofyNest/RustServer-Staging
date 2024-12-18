#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

public class SprayCan : HeldEntity
{
	private enum SprayFailReason
	{
		None,
		MountedBlocked,
		IOConnection,
		LineOfSight,
		SkinNotOwned,
		InvalidItem
	}

	private struct ContainerSet
	{
		public int ContainerIndex;

		public uint PrefabId;
	}

	public struct ChildPreserveInfo
	{
		public BaseEntity TargetEntity;

		public uint TargetBone;

		public Vector3 LocalPosition;

		public Quaternion LocalRotation;
	}

	public const float MaxFreeSprayDistanceFromStart = 10f;

	public const float MaxFreeSprayStartingDistance = 3f;

	private SprayCanSpray_Freehand paintingLine;

	public const Flags IsFreeSpraying = Flags.Reserved1;

	public SoundDefinition SpraySound;

	public GameObjectRef SkinSelectPanel;

	public float SprayCooldown = 2f;

	public float ConditionLossPerSpray = 10f;

	public float ConditionLossPerReskin = 10f;

	public GameObjectRef LinePrefab;

	public Color[] SprayColours = new Color[0];

	public float[] SprayWidths = new float[3] { 0.1f, 0.2f, 0.3f };

	public ParticleSystem worldSpaceSprayFx;

	public GameObjectRef ReskinEffect;

	public ItemDefinition SprayDecalItem;

	public GameObjectRef SprayDecalEntityRef;

	public SteamInventoryItem FreeSprayUnlockItem;

	public ParticleSystem.MinMaxGradient DecalSprayGradient;

	public SoundDefinition SprayLoopDef;

	public static Translate.Phrase FreeSprayNamePhrase = new Translate.Phrase("freespray_radial", "Free Spray");

	public static Translate.Phrase FreeSprayDescPhrase = new Translate.Phrase("freespray_radial_desc", "Spray shapes freely with various colors");

	public static Translate.Phrase BuildingSkinColourPhrase = new Translate.Phrase("buildingskin_colour", "Set colour");

	public static Translate.Phrase BuildingSkinColourDescPhrase = new Translate.Phrase("buildingskin_colour_desc", "Set the block to the highlighted colour");

	public static readonly Translate.Phrase DoorMustBeClosed = new Translate.Phrase("error_doormustbeclosed", "Door must be closed");

	public static readonly Translate.Phrase NeedDoorAccess = new Translate.Phrase("error_needdooraccess", "Need door access");

	public static readonly Translate.Phrase CannotReskinThatDoor = new Translate.Phrase("error_cannotreskindoor", "Cannot reskin that door");

	[FormerlySerializedAs("ShippingCOntainerColourLookup")]
	public ConstructionSkin_ColourLookup ShippingContainerColourLookup;

	public const string ENEMY_BASE_STAT = "sprayed_enemy_base";

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("SprayCan.OnRpcMessage"))
		{
			if (rpc == 3490735573u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - BeginFreehandSpray ");
				}
				using (TimeWarning.New("BeginFreehandSpray"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsActiveItem.Test(3490735573u, "BeginFreehandSpray", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg2 = rPCMessage;
							BeginFreehandSpray(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in BeginFreehandSpray");
					}
				}
				return true;
			}
			if (rpc == 151738090 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ChangeItemSkin ");
				}
				using (TimeWarning.New("ChangeItemSkin"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(151738090u, "ChangeItemSkin", this, player, 2uL))
						{
							return true;
						}
						if (!RPC_Server.IsActiveItem.Test(151738090u, "ChangeItemSkin", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg3 = rPCMessage;
							ChangeItemSkin(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in ChangeItemSkin");
					}
				}
				return true;
			}
			if (rpc == 688080035 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - ChangeWallpaper ");
				}
				using (TimeWarning.New("ChangeWallpaper"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(688080035u, "ChangeWallpaper", this, player, 2uL))
						{
							return true;
						}
						if (!RPC_Server.IsActiveItem.Test(688080035u, "ChangeWallpaper", this, player))
						{
							return true;
						}
						if (!RPC_Server.MaxDistance.Test(688080035u, "ChangeWallpaper", this, player, 5f))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg4 = rPCMessage;
							ChangeWallpaper(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in ChangeWallpaper");
					}
				}
				return true;
			}
			if (rpc == 396000799 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - CreateSpray ");
				}
				using (TimeWarning.New("CreateSpray"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsActiveItem.Test(396000799u, "CreateSpray", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg5 = rPCMessage;
							CreateSpray(msg5);
						}
					}
					catch (Exception exception4)
					{
						Debug.LogException(exception4);
						player.Kick("RPC Error in CreateSpray");
					}
				}
				return true;
			}
			if (rpc == 14517645 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - Server_SetBlockColourId ");
				}
				using (TimeWarning.New("Server_SetBlockColourId"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(14517645u, "Server_SetBlockColourId", this, player, 3uL))
						{
							return true;
						}
						if (!RPC_Server.IsActiveItem.Test(14517645u, "Server_SetBlockColourId", this, player))
						{
							return true;
						}
					}
					try
					{
						using (TimeWarning.New("Call"))
						{
							RPCMessage rPCMessage = default(RPCMessage);
							rPCMessage.connection = msg.connection;
							rPCMessage.player = player;
							rPCMessage.read = msg.read;
							RPCMessage msg6 = rPCMessage;
							Server_SetBlockColourId(msg6);
						}
					}
					catch (Exception exception5)
					{
						Debug.LogException(exception5);
						player.Kick("RPC Error in Server_SetBlockColourId");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	private void BeginFreehandSpray(RPCMessage msg)
	{
		if (!IsBusy() && CanSprayFreehand(msg.player))
		{
			Vector3 vector = msg.read.Vector3();
			Vector3 atNormal = msg.read.Vector3();
			int num = msg.read.Int32();
			int num2 = msg.read.Int32();
			if (num >= 0 && num < SprayColours.Length && num2 >= 0 && num2 < SprayWidths.Length && !(Vector3.Distance(vector, GetOwnerPlayer().transform.position) > 3f))
			{
				SprayCanSpray_Freehand sprayCanSpray_Freehand = GameManager.server.CreateEntity(LinePrefab.resourcePath, vector, Quaternion.identity) as SprayCanSpray_Freehand;
				sprayCanSpray_Freehand.AddInitialPoint(atNormal);
				sprayCanSpray_Freehand.SetColour(SprayColours[num]);
				sprayCanSpray_Freehand.SetWidth(SprayWidths[num2]);
				sprayCanSpray_Freehand.EnableChanges(msg.player);
				sprayCanSpray_Freehand.Spawn();
				paintingLine = sprayCanSpray_Freehand;
				ClientRPC(RpcTarget.NetworkGroup("Client_ChangeSprayColour"), num);
				SetFlag(Flags.Busy, b: true);
				SetFlag(Flags.Reserved1, b: true);
				CheckAchievementPosition(vector);
			}
		}
	}

	public void ClearPaintingLine(bool allowNewSprayImmediately)
	{
		paintingLine = null;
		LoseCondition(ConditionLossPerSpray);
		if (allowNewSprayImmediately)
		{
			ClearBusy();
		}
		else
		{
			Invoke(ClearBusy, 0.1f);
		}
	}

	public bool CanSprayFreehand(BasePlayer player)
	{
		if (player.UnlockAllSkins)
		{
			return true;
		}
		if (FreeSprayUnlockItem != null)
		{
			if (!player.blueprints.steamInventory.HasItem(FreeSprayUnlockItem.id))
			{
				return FreeSprayUnlockItem.HasUnlocked(player.userID);
			}
			return true;
		}
		return false;
	}

	private bool IsSprayBlockedByTrigger(Vector3 pos)
	{
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (ownerPlayer == null)
		{
			return true;
		}
		TriggerNoSpray triggerNoSpray = ownerPlayer.FindTrigger<TriggerNoSpray>();
		if (triggerNoSpray == null)
		{
			return false;
		}
		return !triggerNoSpray.IsPositionValid(pos);
	}

	private bool ValidateEntityAndSkin(BasePlayer player, BaseNetworkable targetEnt, int targetSkin)
	{
		if (IsBusy())
		{
			return false;
		}
		if (player == null || !player.CanBuild())
		{
			return false;
		}
		bool unlockAllSkins = player.UnlockAllSkins;
		if (targetSkin != 0 && !unlockAllSkins && !player.blueprints.CheckSkinOwnership(targetSkin, player.userID))
		{
			SprayFailResponse(SprayFailReason.SkinNotOwned);
			return false;
		}
		if (targetEnt != null && targetEnt is BaseEntity baseEntity)
		{
			Vector3 position = baseEntity.WorldSpaceBounds().ClosestPoint(player.eyes.position);
			if (!player.IsVisible(position, 3f))
			{
				SprayFailResponse(SprayFailReason.LineOfSight);
				return false;
			}
			if (targetEnt is Door door)
			{
				if (!door.GetPlayerLockPermission(player))
				{
					player.ShowToast(GameTip.Styles.Error, NeedDoorAccess, false);
					return false;
				}
				if (door.IsOpen())
				{
					player.ShowToast(GameTip.Styles.Error, DoorMustBeClosed, false);
					return false;
				}
				if (door.GetParentEntity() != null && door.GetParentEntity() is HotAirBalloonArmor)
				{
					player.ShowToast(GameTip.Styles.Error, CannotReskinThatDoor, false);
					return false;
				}
			}
		}
		return true;
	}

	private void SprayFailResponse(SprayFailReason reason)
	{
		ClientRPC(RpcTarget.NetworkGroup("Client_ReskinResult"), 0, (int)reason);
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	[RPC_Server.CallsPerSecond(2uL)]
	private void ChangeItemSkin(RPCMessage msg)
	{
		NetworkableId uid = msg.read.EntityID();
		int targetSkin = msg.read.Int32();
		BaseNetworkable baseNetworkable = BaseNetworkable.serverEntities.Find(uid);
		if (!ValidateEntityAndSkin(msg.player, baseNetworkable, targetSkin))
		{
			return;
		}
		if (baseNetworkable != null)
		{
			BaseEntity baseEntity2 = baseNetworkable as BaseEntity;
			if ((object)baseEntity2 != null)
			{
				if (!GetItemDefinitionForEntity(baseEntity2, out var def, useRedirect: false))
				{
					FailResponse(SprayFailReason.InvalidItem);
					return;
				}
				ItemDefinition itemDefinition = null;
				ulong num = ItemDefinition.FindSkin((def.isRedirectOf != null) ? def.isRedirectOf.itemid : def.itemid, targetSkin);
				ItemSkinDirectory.Skin skin = def.skins.FirstOrDefault((ItemSkinDirectory.Skin x) => x.id == targetSkin);
				if (skin.invItem != null && skin.invItem is ItemSkin itemSkin)
				{
					if (itemSkin.Redirect != null)
					{
						itemDefinition = itemSkin.Redirect;
					}
					else if ((bool)def && def.isRedirectOf != null)
					{
						itemDefinition = def.isRedirectOf;
					}
				}
				else if (def.isRedirectOf != null || ((bool)def && def.isRedirectOf != null))
				{
					itemDefinition = def.isRedirectOf;
				}
				if (itemDefinition == null)
				{
					baseEntity2.skinID = num;
					baseEntity2.SendNetworkUpdate();
					Analytics.Server.SkinUsed(def.shortname, targetSkin);
					Analytics.Azure.OnEntitySkinChanged(msg.player, baseNetworkable, targetSkin);
				}
				else
				{
					if (!CanEntityBeRespawned(baseEntity2, out var reason2))
					{
						FailResponse(reason2);
						return;
					}
					if (!GetEntityPrefabPath(itemDefinition, out var resourcePath))
					{
						Debug.LogWarning("Cannot find resource path of redirect entity to spawn! " + itemDefinition.gameObject.name);
						FailResponse(SprayFailReason.InvalidItem);
						return;
					}
					Vector3 localPosition = baseEntity2.transform.localPosition;
					Quaternion localRotation = baseEntity2.transform.localRotation;
					BaseEntity baseEntity3 = baseEntity2.GetParentEntity();
					float health = baseEntity2.Health();
					EntityRef[] slots = baseEntity2.GetSlots();
					ulong ownerID = baseEntity2.OwnerID;
					float lastAttackedTime = ((baseEntity2 is BaseCombatEntity baseCombatEntity) ? baseCombatEntity.lastAttackedTime : 0f);
					HashSet<PlayerNameID> hashSet = null;
					if (baseEntity2 is BuildingPrivlidge buildingPrivlidge)
					{
						hashSet = new HashSet<PlayerNameID>(buildingPrivlidge.authorizedPlayers);
					}
					bool flag = baseEntity2 is Door || baseEntity2 is BuildingPrivlidge;
					Dictionary<ContainerSet, List<Item>> dictionary2 = new Dictionary<ContainerSet, List<Item>>();
					SaveEntityStorage(baseEntity2, dictionary2, 0);
					List<ChildPreserveInfo> obj = Facepunch.Pool.Get<List<ChildPreserveInfo>>();
					if (flag)
					{
						foreach (BaseEntity child in baseEntity2.children)
						{
							obj.Add(new ChildPreserveInfo
							{
								TargetEntity = child,
								TargetBone = child.parentBone,
								LocalPosition = child.transform.localPosition,
								LocalRotation = child.transform.localRotation
							});
						}
						foreach (ChildPreserveInfo item in obj)
						{
							item.TargetEntity.SetParent(null, worldPositionStays: true);
						}
					}
					else
					{
						for (int i = 0; i < baseEntity2.children.Count; i++)
						{
							SaveEntityStorage(baseEntity2.children[i], dictionary2, -1);
						}
					}
					baseEntity2.Kill();
					baseEntity2 = GameManager.server.CreateEntity(resourcePath, (baseEntity3 != null) ? baseEntity3.transform.TransformPoint(localPosition) : localPosition, (baseEntity3 != null) ? (baseEntity3.transform.rotation * localRotation) : localRotation);
					baseEntity2.SetParent(baseEntity3);
					baseEntity2.transform.localPosition = localPosition;
					baseEntity2.transform.localRotation = localRotation;
					baseEntity2.OwnerID = ownerID;
					if (GetItemDefinitionForEntity(baseEntity2, out var def2, useRedirect: false) && def2.isRedirectOf != null)
					{
						baseEntity2.skinID = 0uL;
					}
					else
					{
						baseEntity2.skinID = num;
					}
					if (baseEntity2 is DecayEntity decayEntity)
					{
						decayEntity.AttachToBuilding(null);
					}
					baseEntity2.Spawn();
					if (baseEntity2 is BaseCombatEntity baseCombatEntity2)
					{
						baseCombatEntity2.SetHealth(health);
						baseCombatEntity2.lastAttackedTime = lastAttackedTime;
					}
					if (baseEntity2 is BuildingPrivlidge buildingPrivlidge2 && hashSet != null)
					{
						buildingPrivlidge2.authorizedPlayers = hashSet;
					}
					if (dictionary2.Count > 0)
					{
						RestoreEntityStorage(baseEntity2, 0, dictionary2);
						if (!flag)
						{
							for (int j = 0; j < baseEntity2.children.Count; j++)
							{
								RestoreEntityStorage(baseEntity2.children[j], -1, dictionary2);
							}
						}
						foreach (KeyValuePair<ContainerSet, List<Item>> item2 in dictionary2)
						{
							foreach (Item item3 in item2.Value)
							{
								Debug.Log($"Deleting {item3} as it has no new container");
								item3.Remove();
							}
						}
						Analytics.Server.SkinUsed(def.shortname, targetSkin);
						Analytics.Azure.OnEntitySkinChanged(msg.player, baseNetworkable, targetSkin);
					}
					if (flag)
					{
						foreach (ChildPreserveInfo item4 in obj)
						{
							item4.TargetEntity.SetParent(baseEntity2, item4.TargetBone, worldPositionStays: true);
							item4.TargetEntity.transform.localPosition = item4.LocalPosition;
							item4.TargetEntity.transform.localRotation = item4.LocalRotation;
							item4.TargetEntity.SendNetworkUpdate();
						}
						baseEntity2.SetSlots(slots);
					}
					Facepunch.Pool.FreeUnmanaged(ref obj);
				}
				ClientRPC(RpcTarget.NetworkGroup("Client_ReskinResult"), 1, baseEntity2.net.ID);
			}
		}
		LoseCondition(ConditionLossPerReskin);
		ClientRPC(RpcTarget.NetworkGroup("Client_ChangeSprayColour"), -1);
		SetFlag(Flags.Busy, b: true);
		Invoke(ClearBusy, SprayCooldown);
		void FailResponse(SprayFailReason reason)
		{
			ClientRPC(RpcTarget.NetworkGroup("Client_ReskinResult"), 0, (int)reason);
		}
		static void RestoreEntityStorage(BaseEntity baseEntity, int index, Dictionary<ContainerSet, List<Item>> copy)
		{
			if (baseEntity is IItemContainerEntity itemContainerEntity)
			{
				ContainerSet containerSet = default(ContainerSet);
				containerSet.ContainerIndex = index;
				containerSet.PrefabId = ((index != 0) ? baseEntity.prefabID : 0u);
				ContainerSet key = containerSet;
				if (copy.ContainsKey(key))
				{
					foreach (Item item5 in copy[key])
					{
						item5.MoveToContainer(itemContainerEntity.inventory);
					}
					copy.Remove(key);
				}
			}
		}
		static void SaveEntityStorage(BaseEntity baseEntity, Dictionary<ContainerSet, List<Item>> dictionary, int index)
		{
			if (baseEntity is IItemContainerEntity itemContainerEntity2)
			{
				ContainerSet containerSet2 = default(ContainerSet);
				containerSet2.ContainerIndex = index;
				containerSet2.PrefabId = ((index != 0) ? baseEntity.prefabID : 0u);
				ContainerSet key2 = containerSet2;
				if (!dictionary.ContainsKey(key2))
				{
					dictionary.Add(key2, new List<Item>());
					foreach (Item item6 in itemContainerEntity2.inventory.itemList)
					{
						dictionary[key2].Add(item6);
					}
					{
						foreach (Item item7 in dictionary[key2])
						{
							item7.RemoveFromContainer();
						}
						return;
					}
				}
				Debug.Log("Multiple containers with the same prefab id being added during vehicle reskin");
			}
		}
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	[RPC_Server.CallsPerSecond(2uL)]
	[RPC_Server.MaxDistance(5f)]
	private void ChangeWallpaper(RPCMessage msg)
	{
		NetworkableId uid = msg.read.EntityID();
		int num = msg.read.Int32();
		bool flag = msg.read.Bool();
		BaseNetworkable baseNetworkable = BaseNetworkable.serverEntities.Find(uid);
		if (ValidateEntityAndSkin(msg.player, baseNetworkable, num))
		{
			if (!(baseNetworkable is BuildingBlock buildingBlock) || !buildingBlock.HasWallpaper())
			{
				SprayFailResponse(SprayFailReason.InvalidItem);
				return;
			}
			ulong id = ItemDefinition.FindSkin(WallpaperPlanner.WallpaperItemDef.itemid, num);
			buildingBlock.SetWallpaper(id, (!flag) ? 1 : 0);
			Analytics.Server.SkinUsed(WallpaperPlanner.WallpaperItemDef.shortname, num);
			Analytics.Azure.OnWallpaperPlaced(msg.player, buildingBlock, id, (!flag) ? 1 : 0, reskin: true);
			ClientRPC(RpcTarget.NetworkGroup("Client_ReskinResult"), 1, buildingBlock.net.ID);
			SetFlag(Flags.Busy, b: true);
			Invoke(ClearBusy, SprayCooldown);
		}
	}

	private bool GetEntityPrefabPath(ItemDefinition def, out string resourcePath)
	{
		resourcePath = string.Empty;
		if (def.TryGetComponent<ItemModDeployable>(out var component))
		{
			resourcePath = component.entityPrefab.resourcePath;
			return true;
		}
		if (def.TryGetComponent<ItemModEntity>(out var component2))
		{
			resourcePath = component2.entityPrefab.resourcePath;
			return true;
		}
		if (def.TryGetComponent<ItemModEntityReference>(out var component3))
		{
			resourcePath = component3.entityPrefab.resourcePath;
			return true;
		}
		return false;
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	private void CreateSpray(RPCMessage msg)
	{
		if (IsBusy())
		{
			return;
		}
		ClientRPC(RpcTarget.NetworkGroup("Client_ChangeSprayColour"), -1);
		SetFlag(Flags.Busy, b: true);
		Invoke(ClearBusy, SprayCooldown);
		Vector3 vector = msg.read.Vector3();
		Vector3 vector2 = msg.read.Vector3();
		Vector3 point = msg.read.Vector3();
		int num = msg.read.Int32();
		if (!(Vector3.Distance(vector, base.transform.position) > 4.5f))
		{
			Quaternion rot = Quaternion.LookRotation((new Plane(vector2, vector).ClosestPointOnPlane(point) - vector).normalized, vector2);
			rot *= Quaternion.Euler(0f, 0f, 90f);
			bool flag = false;
			if (msg.player.IsDeveloper)
			{
				flag = true;
			}
			if (num != 0 && !flag && !msg.player.blueprints.CheckSkinOwnership(num, msg.player.userID))
			{
				Debug.Log($"SprayCan.ChangeItemSkin player does not have item :{num}:");
				return;
			}
			ulong num2 = ItemDefinition.FindSkin(SprayDecalItem.itemid, num);
			BaseEntity baseEntity = GameManager.server.CreateEntity(SprayDecalEntityRef.resourcePath, vector, rot);
			baseEntity.skinID = num2;
			baseEntity.OnDeployed(null, GetOwnerPlayer(), GetItem());
			baseEntity.Spawn();
			CheckAchievementPosition(vector);
			LoseCondition(ConditionLossPerSpray);
		}
	}

	private void CheckAchievementPosition(Vector3 pos)
	{
	}

	private void LoseCondition(float amount)
	{
		GetOwnerItem()?.LoseCondition(amount);
	}

	public void ClearBusy()
	{
		SetFlag(Flags.Busy, b: false);
		SetFlag(Flags.Reserved1, b: false);
	}

	public override void OnHeldChanged()
	{
		if (IsDisabled())
		{
			ClearBusy();
			if (paintingLine != null)
			{
				paintingLine.Kill();
			}
			paintingLine = null;
		}
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	[RPC_Server.CallsPerSecond(3uL)]
	private void Server_SetBlockColourId(RPCMessage msg)
	{
		NetworkableId uid = msg.read.EntityID();
		uint num = msg.read.UInt32();
		BasePlayer player = msg.player;
		SetFlag(Flags.Busy, b: true);
		Invoke(ClearBusy, 0.1f);
		if (!(player == null) && player.CanBuild())
		{
			BasePlayer ownerPlayer = GetOwnerPlayer();
			BuildingBlock buildingBlock = BaseNetworkable.serverEntities.Find(uid) as BuildingBlock;
			if (buildingBlock != null && !(player.Distance(buildingBlock) > 4f))
			{
				uint customColour = buildingBlock.customColour;
				buildingBlock.SetCustomColour(num);
				Analytics.Azure.OnBuildingBlockColorChanged(ownerPlayer, buildingBlock, customColour, num);
			}
		}
	}

	private bool CanEntityBeRespawned(BaseEntity targetEntity, out SprayFailReason reason)
	{
		if (targetEntity is BaseMountable baseMountable && baseMountable.AnyMounted())
		{
			reason = SprayFailReason.MountedBlocked;
			return false;
		}
		if (targetEntity.isServer && targetEntity is BaseVehicle baseVehicle && (baseVehicle.HasDriver() || baseVehicle.AnyMounted()))
		{
			reason = SprayFailReason.MountedBlocked;
			return false;
		}
		if (targetEntity is IOEntity iOEntity && (iOEntity.GetConnectedInputCount() != 0 || iOEntity.GetConnectedOutputCount() != 0))
		{
			reason = SprayFailReason.IOConnection;
			return false;
		}
		reason = SprayFailReason.None;
		return true;
	}

	public static bool GetItemDefinitionForEntity(BaseEntity be, out ItemDefinition def, bool useRedirect = true)
	{
		def = null;
		if (be is BaseCombatEntity baseCombatEntity)
		{
			if (baseCombatEntity.pickup.enabled && baseCombatEntity.pickup.itemTarget != null)
			{
				def = baseCombatEntity.pickup.itemTarget;
			}
			else if (baseCombatEntity.repair.enabled && baseCombatEntity.repair.itemTarget != null)
			{
				def = baseCombatEntity.repair.itemTarget;
			}
		}
		if (useRedirect && def != null && def.isRedirectOf != null)
		{
			def = def.isRedirectOf;
		}
		return def != null;
	}
}
