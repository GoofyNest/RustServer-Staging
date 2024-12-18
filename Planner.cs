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

public class Planner : HeldEntity
{
	public struct CanBuildResult
	{
		public bool Result;

		public Translate.Phrase Phrase;

		public string[] Arguments;
	}

	public BaseEntity[] buildableList;

	public virtual bool isTypeDeployable => GetModDeployable() != null;

	public Vector3 serverStartDurationPlacementPosition { get; private set; }

	public TimeSince serverStartDurationPlacementTime { get; private set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("Planner.OnRpcMessage"))
		{
			if (rpc == 1872774636 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - DoPlace ");
				}
				using (TimeWarning.New("DoPlace"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.IsActiveItem.Test(1872774636u, "DoPlace", this, player))
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
							DoPlace(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in DoPlace");
					}
				}
				return true;
			}
			if (rpc == 3892284151u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - StartDurationPlace ");
				}
				using (TimeWarning.New("StartDurationPlace"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.CallsPerSecond.Test(3892284151u, "StartDurationPlace", this, player, 10uL))
						{
							return true;
						}
						if (!RPC_Server.IsActiveItem.Test(3892284151u, "StartDurationPlace", this, player))
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
							StartDurationPlace(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in StartDurationPlace");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public ItemModDeployable GetModDeployable()
	{
		ItemDefinition ownerItemDefinition = GetOwnerItemDefinition();
		if (ownerItemDefinition == null)
		{
			return null;
		}
		return ownerItemDefinition.GetComponent<ItemModDeployable>();
	}

	public virtual Deployable GetDeployable()
	{
		ItemModDeployable modDeployable = GetModDeployable();
		if (modDeployable == null)
		{
			return null;
		}
		return modDeployable.GetDeployable(this);
	}

	public virtual Deployable GetDeployable(NetworkableId entityId)
	{
		return GetDeployable();
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	private void DoPlace(RPCMessage msg)
	{
		if (!msg.player.CanInteract())
		{
			return;
		}
		using CreateBuilding msg2 = CreateBuilding.Deserialize(msg.read);
		DoBuild(msg2);
	}

	[RPC_Server]
	[RPC_Server.IsActiveItem]
	[RPC_Server.CallsPerSecond(10uL)]
	private void StartDurationPlace(RPCMessage msg)
	{
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if ((bool)ownerPlayer)
		{
			serverStartDurationPlacementPosition = ownerPlayer.transform.position;
			serverStartDurationPlacementTime = 0f;
		}
	}

	public Socket_Base FindSocket(string name, uint prefabIDToFind)
	{
		return PrefabAttribute.server.FindAll<Socket_Base>(prefabIDToFind).FirstOrDefault((Socket_Base s) => s.socketName == name);
	}

	public virtual void DoBuild(CreateBuilding msg)
	{
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (!ownerPlayer)
		{
			return;
		}
		if (ConVar.AntiHack.objectplacement && ownerPlayer.TriggeredAntiHack())
		{
			ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.AntihackWithReason, false, ownerPlayer.lastViolationType.ToString());
			return;
		}
		Construction construction = PrefabAttribute.server.Find<Construction>(msg.blockID);
		if (construction == null)
		{
			ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.CouldntFindConstruction, false);
			ConstructionErrors.Log(ownerPlayer, msg.blockID.ToString());
			return;
		}
		if (!CanAffordToPlace(construction))
		{
			using (ItemAmountList itemAmountList = Facepunch.Pool.Get<ItemAmountList>())
			{
				itemAmountList.amount = Facepunch.Pool.Get<List<float>>();
				itemAmountList.itemID = Facepunch.Pool.Get<List<int>>();
				GetConstructionCost(itemAmountList, construction);
				ownerPlayer.ClientRPC(RpcTarget.Player("Client_OnRepairFailedResources", ownerPlayer), itemAmountList);
				return;
			}
		}
		if (!ownerPlayer.CanBuild() && !construction.canBypassBuildingPermission)
		{
			ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.NoPermission, false);
			return;
		}
		Deployable deployable = GetDeployable(msg.entity);
		if (construction.deployable != deployable)
		{
			ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.DeployableMismatch, false);
			return;
		}
		Construction.Target target = default(Construction.Target);
		if (msg.entity.IsValid)
		{
			target.entity = BaseNetworkable.serverEntities.Find(msg.entity) as BaseEntity;
			if (target.entity == null)
			{
				ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.CouldntFindEntity, false);
				ConstructionErrors.Log(ownerPlayer, msg.entity.ToString());
				return;
			}
			msg.ray = new Ray(target.entity.transform.TransformPoint(msg.ray.origin), target.entity.transform.TransformDirection(msg.ray.direction));
			msg.position = target.entity.transform.TransformPoint(msg.position);
			msg.normal = target.entity.transform.TransformDirection(msg.normal);
			msg.rotation = target.entity.transform.rotation * msg.rotation;
			if (msg.socket != 0)
			{
				string text = StringPool.Get(msg.socket);
				if (text != "")
				{
					target.socket = FindSocket(text, target.entity.prefabID);
				}
				if (target.socket == null)
				{
					ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.CouldntFindSocket, false);
					ConstructionErrors.Log(ownerPlayer, msg.socket.ToString());
					return;
				}
			}
			else if (target.entity is Door)
			{
				ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.CantDeployOnDoor, false);
				return;
			}
		}
		target.ray = msg.ray;
		target.onTerrain = msg.onterrain;
		target.position = msg.position;
		target.normal = msg.normal;
		target.rotation = msg.rotation;
		target.player = ownerPlayer;
		target.isHoldingShift = msg.isHoldingShift;
		target.valid = true;
		if (ShouldParent(target.entity, deployable))
		{
			Vector3 position = ((target.socket != null) ? target.GetWorldPosition() : target.position);
			float num = target.entity.Distance(position);
			if (num > 1f)
			{
				ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.ParentTooFar, false);
				ConstructionErrors.Log(ownerPlayer, num.ToString());
				return;
			}
		}
		BaseEntity baseEntity = DoBuild(target, construction);
		if (baseEntity != null && baseEntity is BuildingBlock buildingBlock && ownerPlayer.IsInCreativeMode && Creative.freeBuild)
		{
			ConstructionGrade constructionGrade = construction.grades[msg.setToGrade];
			if (buildingBlock.currentGrade != constructionGrade)
			{
				buildingBlock.ChangeGradeAndSkin(constructionGrade.gradeBase.type, constructionGrade.gradeBase.skin);
			}
		}
		if (baseEntity != null && baseEntity is DecayEntity decayEntity)
		{
			decayEntity.timePlaced = GetNetworkTime();
		}
	}

	public virtual BaseEntity DoBuild(Construction.Target target, Construction component)
	{
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (!ownerPlayer)
		{
			return null;
		}
		if (target.ray.IsNaNOrInfinity())
		{
			return null;
		}
		if (target.position.IsNaNOrInfinity())
		{
			return null;
		}
		if (target.normal.IsNaNOrInfinity())
		{
			return null;
		}
		Construction.lastPlacementError = "";
		Construction.lastPlacementErrorDebug = "";
		Construction.lastBuildingBlockError = null;
		Construction.lastPlacementErrorIsDetailed = false;
		if (target.socket != null)
		{
			if (!target.socket.female)
			{
				ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.SocketNotFemale, false);
				Construction.lastPlacementErrorDebug = target.socket.socketName;
				return null;
			}
			if (target.entity != null && target.entity.IsOccupied(target.socket))
			{
				ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.SocketOccupied, false);
				Construction.lastPlacementErrorDebug = target.socket.socketName;
				return null;
			}
			if (target.onTerrain)
			{
				Construction.lastPlacementErrorDebug = "Target on terrain is not allowed when attaching to socket (" + target.socket.socketName + ")";
				return null;
			}
		}
		Vector3 deployPos = ((target.entity != null && target.socket != null) ? target.GetWorldPosition() : target.position);
		if (AntiHack.TestIsBuildingInsideSomething(target, deployPos))
		{
			ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.InsideObjects, false);
			return null;
		}
		if (ConVar.AntiHack.eye_protection >= 2 && !HasLineOfSight(ownerPlayer, deployPos, target, component))
		{
			ownerPlayer.ShowToast(GameTip.Styles.Error, ConstructionErrors.LineOfSightBlocked, false);
			return null;
		}
		if (ConVar.Server.max_sleeping_bags > 0)
		{
			CanBuildResult? result = SleepingBag.CanBuildBed(ownerPlayer, component);
			if (HandleCanBuild(result, ownerPlayer))
			{
				return null;
			}
		}
		GameObject gameObject = DoPlacement(target, component);
		if (gameObject == null)
		{
			if (!string.IsNullOrEmpty(Construction.lastPlacementError.translated))
			{
				ownerPlayer.ShowToast(GameTip.Styles.Error, Construction.lastPlacementError, false);
			}
			ConstructionErrors.Log(ownerPlayer, Construction.lastPlacementErrorDebug);
		}
		if (gameObject != null)
		{
			Deployable deployable = GetDeployable();
			BaseEntity baseEntity = gameObject.ToBaseEntity();
			if (baseEntity != null && deployable != null)
			{
				if (ShouldParent(target.entity, deployable))
				{
					if (target.socket is Socket_Specific_Female socket_Specific_Female)
					{
						if (socket_Specific_Female.parentToBone)
						{
							baseEntity.SetParent(target.entity, socket_Specific_Female.boneName, worldPositionStays: true);
						}
						else
						{
							baseEntity.SetParent(target.entity, worldPositionStays: true);
						}
					}
					else
					{
						baseEntity.SetParent(target.entity, worldPositionStays: true);
					}
				}
				if (deployable.wantsInstanceData && GetOwnerItem().instanceData != null)
				{
					(baseEntity as IInstanceDataReceiver).ReceiveInstanceData(GetOwnerItem().instanceData);
				}
				if (deployable.copyInventoryFromItem)
				{
					StorageContainer component2 = baseEntity.GetComponent<StorageContainer>();
					if ((bool)component2)
					{
						component2.ReceiveInventoryFromItem(GetOwnerItem());
					}
				}
				ItemModDeployable modDeployable = GetModDeployable();
				if (modDeployable != null)
				{
					modDeployable.OnDeployed(baseEntity, ownerPlayer);
				}
				baseEntity.OnDeployed(baseEntity.GetParentEntity(), ownerPlayer, GetOwnerItem());
				if (deployable.placeEffect.isValid)
				{
					if ((bool)target.entity && target.socket != null)
					{
						Effect.server.Run(deployable.placeEffect.resourcePath, target.entity.transform.TransformPoint(target.socket.worldPosition), target.entity.transform.up);
					}
					else
					{
						Effect.server.Run(deployable.placeEffect.resourcePath, target.position, target.normal);
					}
				}
			}
			if (baseEntity != null)
			{
				Analytics.Azure.OnEntityBuilt(baseEntity, ownerPlayer);
				if (GetOwnerItemDefinition() != null)
				{
					ownerPlayer.ProcessMissionEvent(BaseMission.MissionEventType.DEPLOY, new BaseMission.MissionEventPayload
					{
						WorldPosition = baseEntity.transform.position,
						UintIdentifier = baseEntity.prefabID,
						IntIdentifier = GetOwnerItemDefinition().itemid
					}, 1f);
				}
			}
			PayForPlacement(ownerPlayer, component);
			return baseEntity;
		}
		return null;
	}

	public GameObject DoPlacement(Construction.Target placement, Construction component)
	{
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (!ownerPlayer)
		{
			return null;
		}
		BaseEntity baseEntity = component.CreateConstruction(placement, bNeedsValidPlacement: true);
		if (!baseEntity)
		{
			return null;
		}
		float num = 1f;
		float num2 = 0f;
		Item ownerItem = GetOwnerItem();
		if (ownerItem != null)
		{
			baseEntity.skinID = ownerItem.skin;
			if (ownerItem.hasCondition)
			{
				num = ownerItem.conditionNormalized;
			}
		}
		baseEntity.gameObject.AwakeFromInstantiate();
		BuildingBlock buildingBlock = baseEntity as BuildingBlock;
		if ((bool)buildingBlock)
		{
			buildingBlock.blockDefinition = PrefabAttribute.server.Find<Construction>(buildingBlock.prefabID);
			if (!buildingBlock.blockDefinition)
			{
				Debug.LogError("Placing a building block that has no block definition!");
				return null;
			}
			buildingBlock.SetGrade(buildingBlock.blockDefinition.defaultGrade.gradeBase.type);
			num2 = buildingBlock.currentGrade.maxHealth;
		}
		BaseCombatEntity baseCombatEntity = baseEntity as BaseCombatEntity;
		if ((bool)baseCombatEntity)
		{
			num2 = ((buildingBlock != null) ? buildingBlock.currentGrade.maxHealth : baseCombatEntity.startHealth);
			baseCombatEntity.ResetLifeStateOnSpawn = false;
			baseCombatEntity.InitializeHealth(num2 * num, num2);
		}
		baseEntity.OnPlaced(ownerPlayer);
		baseEntity.OwnerID = ownerPlayer.userID;
		baseEntity.Spawn();
		if ((bool)buildingBlock)
		{
			Effect.server.Run("assets/bundled/prefabs/fx/build/frame_place.prefab", baseEntity, 0u, Vector3.zero, Vector3.zero);
		}
		StabilityEntity stabilityEntity = baseEntity as StabilityEntity;
		if ((bool)stabilityEntity)
		{
			stabilityEntity.UpdateSurroundingEntities();
		}
		return baseEntity.gameObject;
	}

	public void PayForPlacement(BasePlayer player, Construction component)
	{
		if (player.IsInCreativeMode && Creative.freeBuild)
		{
			return;
		}
		if (player.IsInTutorial)
		{
			TutorialIsland currentTutorialIsland = player.GetCurrentTutorialIsland();
			if (currentTutorialIsland != null)
			{
				currentTutorialIsland.OnPlayerBuiltConstruction(player);
			}
		}
		if (isTypeDeployable)
		{
			GetItem().UseItem();
			return;
		}
		List<Item> obj = Facepunch.Pool.Get<List<Item>>();
		foreach (ItemAmount item in component.defaultGrade.CostToBuild())
		{
			player.inventory.Take(obj, item.itemDef.itemid, (int)item.amount);
			player.Command("note.inv", item.itemDef.itemid, item.amount * -1f);
		}
		foreach (Item item2 in obj)
		{
			item2.Remove();
		}
		Facepunch.Pool.Free(ref obj, freeElements: false);
	}

	public bool CanAffordToPlace(Construction component)
	{
		if (isTypeDeployable)
		{
			return true;
		}
		BasePlayer ownerPlayer = GetOwnerPlayer();
		if (!ownerPlayer)
		{
			return false;
		}
		if (ownerPlayer.IsInCreativeMode && Creative.freeBuild)
		{
			return true;
		}
		foreach (ItemAmount item in component.defaultGrade.CostToBuild())
		{
			if ((float)ownerPlayer.inventory.GetAmount(item.itemDef.itemid) < item.amount)
			{
				return false;
			}
		}
		return true;
	}

	protected void GetConstructionCost(ItemAmountList list, Construction component)
	{
		list.amount.Clear();
		list.itemID.Clear();
		foreach (ItemAmount item in component.defaultGrade.CostToBuild())
		{
			list.itemID.Add(item.itemDef.itemid);
			list.amount.Add((int)item.amount);
		}
	}

	private bool ShouldParent(BaseEntity targetEntity, Deployable deployable)
	{
		if (targetEntity != null && targetEntity.SupportsChildDeployables() && (targetEntity.ForceDeployableSetParent() || (deployable != null && deployable.setSocketParent)))
		{
			return true;
		}
		return false;
	}

	private bool HandleCanBuild(CanBuildResult? result, BasePlayer player)
	{
		if (result.HasValue)
		{
			if (result.Value.Phrase != null && !player.IsInTutorial)
			{
				player.ShowToast((!result.Value.Result) ? GameTip.Styles.Red_Normal : GameTip.Styles.Blue_Long, result.Value.Phrase, overlay: false, result.Value.Arguments);
			}
			if (!result.Value.Result)
			{
				return true;
			}
		}
		return false;
	}

	protected virtual bool HasLineOfSight(BasePlayer player, Vector3 deployPos, Construction.Target target, Construction component)
	{
		Vector3 center = player.eyes.center;
		Vector3 position = player.eyes.position;
		Vector3 origin = target.ray.origin;
		Vector3 p = deployPos;
		int num = 2097152;
		int num2 = 2162688;
		if (ConVar.AntiHack.build_terraincheck)
		{
			num2 |= 0x800000;
		}
		if (ConVar.AntiHack.build_vehiclecheck)
		{
			num2 |= 0x8000000;
		}
		float num3 = ConVar.AntiHack.build_losradius;
		float padding = ConVar.AntiHack.build_losradius + 0.01f;
		int layerMask = num2;
		if (target.socket != null)
		{
			num3 = 0f;
			padding = 0.5f;
			layerMask = num;
		}
		if (component.isSleepingBag)
		{
			num3 = ConVar.AntiHack.build_losradius_sleepingbag;
			padding = ConVar.AntiHack.build_losradius_sleepingbag + 0.01f;
			layerMask = num2;
		}
		if (num3 > 0f)
		{
			p += target.normal.normalized * num3;
		}
		if (target.entity != null)
		{
			DeployShell deployShell = PrefabAttribute.server.Find<DeployShell>(target.entity.prefabID);
			if (deployShell != null)
			{
				p += target.normal.normalized * deployShell.LineOfSightPadding();
			}
		}
		if (GamePhysics.LineOfSightRadius(center, position, layerMask, num3) && GamePhysics.LineOfSightRadius(position, origin, layerMask, num3))
		{
			return GamePhysics.LineOfSightRadius(origin, p, layerMask, num3, 0f, padding);
		}
		return false;
	}
}
