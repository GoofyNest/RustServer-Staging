#define UNITY_ASSERTIONS
using System;
using System.Collections.Generic;
using ConVar;
using Facepunch;
using Facepunch.Rust;
using Network;
using ProtoBuf;
using Rust;
using UnityEngine;
using UnityEngine.Assertions;

public class BuildingBlock : StabilityEntity
{
	public static class BlockFlags
	{
		public const Flags CanRotate = Flags.Reserved1;
	}

	public class UpdateSkinWorkQueue : ObjectWorkQueue<BuildingBlock>
	{
		protected override void RunJob(BuildingBlock entity)
		{
			if (ShouldAdd(entity))
			{
				entity.UpdateSkin(force: true);
			}
		}

		protected override bool ShouldAdd(BuildingBlock entity)
		{
			return entity.IsValid();
		}
	}

	[NonSerialized]
	public Construction blockDefinition;

	private static Vector3[] outsideLookupOffsets = new Vector3[5]
	{
		new Vector3(0f, 1f, 0f).normalized,
		new Vector3(1f, 1f, 0f).normalized,
		new Vector3(-1f, 1f, 0f).normalized,
		new Vector3(0f, 1f, 1f).normalized,
		new Vector3(0f, 1f, -1f).normalized
	};

	private bool forceSkinRefresh;

	private ulong lastSkinID;

	private int lastModelState;

	private uint lastCustomColour;

	private uint playerCustomColourToApply;

	public BuildingGrade.Enum grade;

	private BuildingGrade.Enum lastGrade = BuildingGrade.Enum.None;

	private ConstructionSkin currentSkin;

	private DeferredAction skinChange;

	private MeshRenderer placeholderRenderer;

	private MeshCollider placeholderCollider;

	public static UpdateSkinWorkQueue updateSkinQueueServer = new UpdateSkinWorkQueue();

	public static readonly Translate.Phrase RotateTitle = new Translate.Phrase("rotate", "Rotate");

	public static readonly Translate.Phrase RotateDesc = new Translate.Phrase("rotate_building_desc", "Rotate or flip this block to face a different direction");

	private bool globalNetworkCooldown;

	public bool CullBushes;

	public bool CheckForPipesOnModelChange;

	public OBBComponent AlternativePipeBounds;

	public float wallpaperHealth = -1f;

	public float wallpaperHealth2 = -1f;

	public ProtectionProperties wallpaperProtection;

	public override bool CanBeDemolished => true;

	public int modelState { get; private set; }

	public uint customColour { get; private set; }

	public ConstructionGrade currentGrade
	{
		get
		{
			if (blockDefinition == null)
			{
				Debug.LogWarning($"blockDefinition is null for {base.ShortPrefabName} {grade} {skinID}");
				return null;
			}
			ConstructionGrade constructionGrade = blockDefinition.GetGrade(grade, skinID);
			if (constructionGrade == null)
			{
				Debug.LogWarning($"currentGrade is null for {base.ShortPrefabName} {grade} {skinID}");
				return null;
			}
			return constructionGrade;
		}
	}

	public ulong wallpaperID { get; private set; }

	public ulong wallpaperID2 { get; private set; }

	public override bool OnRpcMessage(BasePlayer player, uint rpc, Message msg)
	{
		using (TimeWarning.New("BuildingBlock.OnRpcMessage"))
		{
			if (rpc == 1956645865 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - DoRotation ");
				}
				using (TimeWarning.New("DoRotation"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(1956645865u, "DoRotation", this, player, 3f))
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
							DoRotation(msg2);
						}
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
						player.Kick("RPC Error in DoRotation");
					}
				}
				return true;
			}
			if (rpc == 3746288057u && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - DoUpgradeToGrade ");
				}
				using (TimeWarning.New("DoUpgradeToGrade"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(3746288057u, "DoUpgradeToGrade", this, player, 3f))
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
							DoUpgradeToGrade(msg3);
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
						player.Kick("RPC Error in DoUpgradeToGrade");
					}
				}
				return true;
			}
			if (rpc == 526349102 && player != null)
			{
				Assert.IsTrue(player.isServer, "SV_RPC Message is using a clientside player!");
				if (ConVar.Global.developer > 2)
				{
					Debug.Log("SV_RPCMessage: " + player?.ToString() + " - RPC_PickupWallpaperStart ");
				}
				using (TimeWarning.New("RPC_PickupWallpaperStart"))
				{
					using (TimeWarning.New("Conditions"))
					{
						if (!RPC_Server.MaxDistance.Test(526349102u, "RPC_PickupWallpaperStart", this, player, 3f))
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
							RPC_PickupWallpaperStart(msg4);
						}
					}
					catch (Exception exception3)
					{
						Debug.LogException(exception3);
						player.Kick("RPC Error in RPC_PickupWallpaperStart");
					}
				}
				return true;
			}
		}
		return base.OnRpcMessage(player, rpc, msg);
	}

	public override void ResetState()
	{
		base.ResetState();
		blockDefinition = null;
		forceSkinRefresh = false;
		modelState = 0;
		lastModelState = 0;
		wallpaperID = 0uL;
		wallpaperID2 = 0uL;
		wallpaperHealth = -1f;
		wallpaperHealth2 = -1f;
		grade = BuildingGrade.Enum.Twigs;
		lastGrade = BuildingGrade.Enum.None;
		DestroySkin();
		UpdatePlaceholder(state: true);
	}

	public override void InitShared()
	{
		base.InitShared();
		placeholderRenderer = GetComponent<MeshRenderer>();
		placeholderCollider = GetComponent<MeshCollider>();
	}

	public override void PostInitShared()
	{
		baseProtection = currentGrade.gradeBase.damageProtecton;
		grade = currentGrade.gradeBase.type;
		base.PostInitShared();
	}

	public override void DestroyShared()
	{
		if (base.isServer)
		{
			RefreshNeighbours(linkToNeighbours: false);
		}
		base.DestroyShared();
	}

	public override string Categorize()
	{
		return "building";
	}

	public override float BoundsPadding()
	{
		return 1f;
	}

	public override bool IsOutside()
	{
		float outside_test_range = ConVar.Decay.outside_test_range;
		Vector3 vector = PivotPoint();
		for (int i = 0; i < outsideLookupOffsets.Length; i++)
		{
			Vector3 vector2 = outsideLookupOffsets[i];
			Vector3 origin = vector + vector2 * outside_test_range;
			if (!UnityEngine.Physics.Raycast(new Ray(origin, -vector2), outside_test_range - 0.5f, 2097152))
			{
				return true;
			}
		}
		return false;
	}

	public override bool SupportsChildDeployables()
	{
		return true;
	}

	public override bool CanReturnEmptyBuildingPrivilege()
	{
		return true;
	}

	public void SetConditionalModel(int state)
	{
		if (state != modelState)
		{
			modelState = state;
			if (base.isServer)
			{
				GlobalNetworkHandler.server?.TrySendNetworkUpdate(this);
			}
		}
	}

	public bool GetConditionalModel(int index)
	{
		return (modelState & (1 << index)) != 0;
	}

	private bool CanChangeToGrade(BuildingGrade.Enum iGrade, ulong iSkin, BasePlayer player)
	{
		if (player.IsInCreativeMode && Creative.freeBuild)
		{
			return true;
		}
		if (HasUpgradePrivilege(iGrade, iSkin, player))
		{
			return !IsUpgradeBlocked();
		}
		return false;
	}

	private bool HasUpgradePrivilege(BuildingGrade.Enum iGrade, ulong iSkin, BasePlayer player)
	{
		if (player.IsInCreativeMode && Creative.freeBuild)
		{
			return true;
		}
		if (iGrade < grade)
		{
			return false;
		}
		if (iGrade == grade && iSkin == skinID)
		{
			return false;
		}
		if (iGrade <= BuildingGrade.Enum.None)
		{
			return false;
		}
		if (iGrade >= BuildingGrade.Enum.Count)
		{
			return false;
		}
		return !player.IsBuildingBlocked(base.transform.position, base.transform.rotation, bounds);
	}

	private bool IsUpgradeBlocked()
	{
		if (!blockDefinition.checkVolumeOnUpgrade)
		{
			return false;
		}
		DeployVolume[] volumes = PrefabAttribute.server.FindAll<DeployVolume>(prefabID);
		return DeployVolume.Check(base.transform.position, base.transform.rotation, volumes, ~(1 << base.gameObject.layer));
	}

	private bool CanAffordUpgrade(BuildingGrade.Enum iGrade, ulong iSkin, BasePlayer player)
	{
		if (player != null && player.IsInCreativeMode && Creative.freeBuild)
		{
			return true;
		}
		foreach (ItemAmount item in blockDefinition.GetGrade(iGrade, iSkin).CostToBuild(grade))
		{
			if ((float)player.inventory.GetAmount(item.itemid) < item.amount)
			{
				return false;
			}
		}
		return true;
	}

	public void SetGrade(BuildingGrade.Enum iGrade)
	{
		if (blockDefinition.grades == null || iGrade <= BuildingGrade.Enum.None || iGrade >= BuildingGrade.Enum.Count)
		{
			Debug.LogError("Tried to set to undefined grade! " + blockDefinition.fullName, base.gameObject);
			return;
		}
		grade = iGrade;
		grade = currentGrade.gradeBase.type;
		UpdateGrade();
	}

	private void UpdateGrade()
	{
		baseProtection = currentGrade.gradeBase.damageProtecton;
	}

	protected override void OnSkinChanged(ulong oldSkinID, ulong newSkinID)
	{
		if (oldSkinID != newSkinID)
		{
			skinID = newSkinID;
		}
	}

	protected override void OnSkinPreProcess(IPrefabProcessor preProcess, GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
	}

	public void SetHealthToMax()
	{
		base.health = MaxHealth();
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void DoUpgradeToGrade(RPCMessage msg)
	{
		if (!msg.player.CanInteract())
		{
			return;
		}
		ConstructionGrade constructionGrade = blockDefinition.GetGrade((BuildingGrade.Enum)msg.read.Int32(), msg.read.UInt64());
		if (constructionGrade == null)
		{
			return;
		}
		if (!CanChangeToGrade(constructionGrade.gradeBase.type, constructionGrade.gradeBase.skin, msg.player))
		{
			if (!(DeployVolume.LastDeployHit != null))
			{
				return;
			}
			BaseEntity baseEntity = DeployVolume.LastDeployHit.ToBaseEntity();
			if (baseEntity != null && baseEntity is BasePlayer basePlayer)
			{
				ulong currentTeam = msg.player.currentTeam;
				if (currentTeam != 0L && currentTeam == basePlayer.currentTeam)
				{
					string playerNameStreamSafe = NameHelper.GetPlayerNameStreamSafe(msg.player, basePlayer);
					msg.player.ShowToast(GameTip.Styles.Error, ConstructionErrors.BlockedByPlayer, false, playerNameStreamSafe);
				}
			}
		}
		else
		{
			if (!CanAffordUpgrade(constructionGrade.gradeBase.type, constructionGrade.gradeBase.skin, msg.player))
			{
				return;
			}
			if (base.SecondsSinceAttacked < 30f)
			{
				msg.player.ShowToast(GameTip.Styles.Error, ConstructionErrors.CantUpgradeRecentlyDamaged, false, (30f - base.SecondsSinceAttacked).ToString("N0"));
				return;
			}
			if (!constructionGrade.gradeBase.alwaysUnlock && constructionGrade.gradeBase.skin != 0L && !msg.player.blueprints.steamInventory.HasItem((int)constructionGrade.gradeBase.skin))
			{
				msg.player.ShowToast(GameTip.Styles.Error, ConstructionErrors.SkinNotOwned, false);
				return;
			}
			PayForUpgrade(constructionGrade, msg.player);
			if (msg.player != null)
			{
				playerCustomColourToApply = GetShippingContainerBlockColourForPlayer(msg.player);
			}
			ClientRPC(RpcTarget.NetworkGroup("DoUpgradeEffect"), (int)constructionGrade.gradeBase.type, constructionGrade.gradeBase.skin);
			BuildingGrade.Enum @enum = grade;
			Analytics.Azure.OnBuildingBlockUpgraded(msg.player, this, constructionGrade.gradeBase.type, playerCustomColourToApply, constructionGrade.gradeBase.skin);
			OnSkinChanged(skinID, constructionGrade.gradeBase.skin);
			ChangeGrade(constructionGrade.gradeBase.type, playEffect: true);
			if (msg.player != null && @enum != constructionGrade.gradeBase.type)
			{
				msg.player.ProcessMissionEvent(BaseMission.MissionEventType.UPGRADE_BUILDING_GRADE, new BaseMission.MissionEventPayload
				{
					NetworkIdentifier = net.ID,
					IntIdentifier = (int)constructionGrade.gradeBase.type
				}, 1f);
			}
			timePlaced = GetNetworkTime();
		}
	}

	private uint GetShippingContainerBlockColourForPlayer(BasePlayer player)
	{
		if (player == null)
		{
			return 0u;
		}
		int infoInt = player.GetInfoInt("client.SelectedShippingContainerBlockColour", 0);
		if (infoInt >= 0)
		{
			return (uint)infoInt;
		}
		return 0u;
	}

	public void ChangeGradeAndSkin(BuildingGrade.Enum targetGrade, ulong skin, bool playEffect = false, bool updateSkin = true)
	{
		OnSkinChanged(skinID, skin);
		ChangeGrade(targetGrade, playEffect, updateSkin);
	}

	public void ChangeGrade(BuildingGrade.Enum targetGrade, bool playEffect = false, bool updateSkin = true)
	{
		SetGrade(targetGrade);
		if (grade != lastGrade)
		{
			SetHealthToMax();
			StartBeingRotatable();
		}
		if (updateSkin)
		{
			UpdateSkin();
		}
		SendNetworkUpdate();
		ResetUpkeepTime();
		UpdateSurroundingEntities();
		GlobalNetworkHandler.server.TrySendNetworkUpdate(this);
		BuildingManager.server.GetBuilding(buildingID)?.Dirty();
	}

	private void PayForUpgrade(ConstructionGrade g, BasePlayer player)
	{
		if (player.IsInCreativeMode && Creative.freeBuild)
		{
			return;
		}
		List<Item> list = new List<Item>();
		foreach (ItemAmount item in g.CostToBuild(grade))
		{
			player.inventory.Take(list, item.itemid, (int)item.amount);
			ItemDefinition itemDefinition = ItemManager.FindItemDefinition(item.itemid);
			Analytics.Azure.LogResource(Analytics.Azure.ResourceMode.Consumed, "upgrade_block", itemDefinition.shortname, (int)item.amount, this, null, safezone: false, null, player.userID);
			player.Command("note.inv " + item.itemid + " " + item.amount * -1f);
		}
		foreach (Item item2 in list)
		{
			item2.Remove();
		}
	}

	public void SetCustomColour(uint newColour)
	{
		if (newColour != customColour)
		{
			customColour = newColour;
			SendNetworkUpdateImmediate();
			ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
			GlobalNetworkHandler.server.TrySendNetworkUpdate(this);
		}
	}

	private bool NeedsSkinChange()
	{
		if (!(currentSkin == null) && !forceSkinRefresh && lastGrade == grade && lastModelState == modelState)
		{
			return lastSkinID != skinID;
		}
		return true;
	}

	public void UpdateSkin(bool force = false)
	{
		if (force)
		{
			forceSkinRefresh = true;
		}
		if (!NeedsSkinChange())
		{
			return;
		}
		if (cachedStability <= 0f || base.isServer)
		{
			ChangeSkin();
			return;
		}
		if (!skinChange)
		{
			skinChange = new DeferredAction(this, ChangeSkin);
		}
		if (skinChange.Idle)
		{
			skinChange.Invoke();
		}
	}

	private void DestroySkin()
	{
		if (currentSkin != null)
		{
			currentSkin.Destroy(this);
			currentSkin = null;
		}
	}

	private void RefreshNeighbours(bool linkToNeighbours)
	{
		List<EntityLink> entityLinks = GetEntityLinks(linkToNeighbours);
		for (int i = 0; i < entityLinks.Count; i++)
		{
			EntityLink entityLink = entityLinks[i];
			for (int j = 0; j < entityLink.connections.Count; j++)
			{
				BuildingBlock buildingBlock = entityLink.connections[j].owner as BuildingBlock;
				if (!(buildingBlock == null))
				{
					if (Rust.Application.isLoading)
					{
						buildingBlock.UpdateSkin(force: true);
					}
					else
					{
						updateSkinQueueServer.Add(buildingBlock);
					}
				}
			}
		}
	}

	private void UpdatePlaceholder(bool state)
	{
		if ((bool)placeholderRenderer)
		{
			placeholderRenderer.enabled = state;
		}
		if ((bool)placeholderCollider)
		{
			placeholderCollider.enabled = state;
		}
	}

	private void ChangeSkin()
	{
		if (base.IsDestroyed)
		{
			return;
		}
		ConstructionGrade constructionGrade = currentGrade;
		if (currentGrade == null)
		{
			Debug.LogWarning("CurrentGrade is null!");
			return;
		}
		if (constructionGrade.skinObject.isValid)
		{
			ChangeSkin(constructionGrade.skinObject);
			return;
		}
		ConstructionGrade defaultGrade = blockDefinition.defaultGrade;
		if (defaultGrade.skinObject.isValid)
		{
			ChangeSkin(defaultGrade.skinObject);
		}
		else
		{
			Debug.LogWarning("No skins found for " + base.gameObject);
		}
	}

	private void ChangeSkin(GameObjectRef prefab)
	{
		bool flag = lastGrade != grade || lastSkinID != skinID;
		lastGrade = grade;
		lastSkinID = skinID;
		if (flag)
		{
			if (currentSkin == null)
			{
				UpdatePlaceholder(state: false);
			}
			else
			{
				DestroySkin();
			}
			GameObject gameObject = base.gameManager.CreatePrefab(prefab.resourcePath, base.transform);
			currentSkin = gameObject.GetComponent<ConstructionSkin>();
			if (currentSkin != null && base.isServer && !Rust.Application.isLoading)
			{
				customColour = currentSkin.GetStartingDetailColour(playerCustomColourToApply);
			}
			Model component = currentSkin.GetComponent<Model>();
			SetModel(component);
			Assert.IsTrue(model == component, "Didn't manage to set model successfully!");
		}
		if (base.isServer)
		{
			SetConditionalModel(currentSkin.DetermineConditionalModelState(this));
		}
		bool flag2 = lastModelState != modelState;
		lastModelState = modelState;
		bool flag3 = lastCustomColour != customColour;
		lastCustomColour = customColour;
		if (flag || flag2 || forceSkinRefresh || flag3)
		{
			currentSkin.Refresh(this);
			if (base.isServer && flag2)
			{
				CheckForPipes();
			}
			forceSkinRefresh = false;
		}
		if (base.isServer)
		{
			if (flag)
			{
				RefreshNeighbours(linkToNeighbours: true);
			}
			if (flag2)
			{
				SendNetworkUpdate();
			}
			timePlaced = GetNetworkTime();
		}
	}

	public override bool ShouldBlockProjectiles()
	{
		return grade != BuildingGrade.Enum.Twigs;
	}

	[ContextMenu("Check for pipes")]
	public void CheckForPipes()
	{
		if (!CheckForPipesOnModelChange || !ConVar.Server.enforcePipeChecksOnBuildingBlockChanges || Rust.Application.isLoading)
		{
			return;
		}
		List<ColliderInfo_Pipe> obj = Facepunch.Pool.Get<List<ColliderInfo_Pipe>>();
		Bounds bounds = base.bounds;
		bounds.extents *= 0.97f;
		Vis.Components((AlternativePipeBounds != null) ? AlternativePipeBounds.GetObb() : new OBB(base.transform, bounds), obj, 536870912);
		foreach (ColliderInfo_Pipe item in obj)
		{
			if (!(item == null) && item.gameObject.activeInHierarchy && item.HasFlag(ColliderInfo.Flags.OnlyBlockBuildingBlock) && item.ParentEntity != null && item.ParentEntity.isServer)
			{
				WireTool.AttemptClearSlot(item.ParentEntity, null, item.OutputSlotIndex, isInput: false);
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	private void OnHammered()
	{
	}

	public override float MaxHealth()
	{
		return currentGrade.maxHealth;
	}

	public override List<ItemAmount> BuildCost()
	{
		return currentGrade.CostToBuild();
	}

	public override void OnHealthChanged(float oldvalue, float newvalue)
	{
		base.OnHealthChanged(oldvalue, newvalue);
		if (base.isServer && Mathf.RoundToInt(oldvalue) != Mathf.RoundToInt(newvalue))
		{
			SendNetworkUpdate(BasePlayer.NetworkQueue.UpdateDistance);
		}
	}

	public override float RepairCostFraction()
	{
		return 1f;
	}

	private bool CanRotate(BasePlayer player)
	{
		if (IsRotatable() && HasRotationPrivilege(player))
		{
			return !IsRotationBlocked();
		}
		return false;
	}

	private bool IsRotatable()
	{
		if (blockDefinition.grades == null)
		{
			return false;
		}
		if (!blockDefinition.canRotateAfterPlacement)
		{
			return false;
		}
		if (!HasFlag(Flags.Reserved1))
		{
			return false;
		}
		return true;
	}

	private bool IsRotationBlocked()
	{
		if (children != null)
		{
			foreach (BaseEntity child in children)
			{
				if (child is TimedExplosive)
				{
					return true;
				}
			}
		}
		if (!blockDefinition.checkVolumeOnRotate)
		{
			return false;
		}
		DeployVolume[] volumes = PrefabAttribute.server.FindAll<DeployVolume>(prefabID);
		return DeployVolume.Check(base.transform.position, base.transform.rotation, volumes, ~(1 << base.gameObject.layer));
	}

	private bool HasRotationPrivilege(BasePlayer player)
	{
		return !player.IsBuildingBlocked(base.transform.position, base.transform.rotation, bounds);
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void DoRotation(RPCMessage msg)
	{
		if (msg.player.CanInteract() && CanRotate(msg.player) && blockDefinition.canRotateAfterPlacement)
		{
			base.transform.localRotation *= Quaternion.Euler(blockDefinition.rotationAmount);
			RefreshEntityLinks();
			UpdateSurroundingEntities();
			UpdateSkin(force: true);
			RefreshNeighbours(linkToNeighbours: false);
			SendNetworkUpdateImmediate();
			ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
			if (!globalNetworkCooldown)
			{
				globalNetworkCooldown = true;
				GlobalNetworkHandler.server.TrySendNetworkUpdate(this);
				CancelInvoke(ResetGlobalNetworkCooldown);
				Invoke(ResetGlobalNetworkCooldown, 15f);
			}
		}
	}

	private void ResetGlobalNetworkCooldown()
	{
		globalNetworkCooldown = false;
		GlobalNetworkHandler.server.TrySendNetworkUpdate(this);
	}

	private void StopBeingRotatable()
	{
		SetFlag(Flags.Reserved1, b: false);
		SendNetworkUpdate();
	}

	private void StartBeingRotatable()
	{
		if (blockDefinition.grades != null && blockDefinition.canRotateAfterPlacement)
		{
			SetFlag(Flags.Reserved1, b: true);
			Invoke(StopBeingRotatable, 600f);
		}
	}

	public override void Save(SaveInfo info)
	{
		base.Save(info);
		info.msg.buildingBlock = Facepunch.Pool.Get<ProtoBuf.BuildingBlock>();
		info.msg.buildingBlock.model = modelState;
		info.msg.buildingBlock.grade = (int)grade;
		info.msg.buildingBlock.wallpaperID = wallpaperID;
		info.msg.buildingBlock.wallpaperID2 = wallpaperID2;
		info.msg.buildingBlock.wallpaperHealth = wallpaperHealth;
		info.msg.buildingBlock.wallpaperHealth2 = wallpaperHealth2;
		if (customColour != 0)
		{
			info.msg.simpleUint = Facepunch.Pool.Get<SimpleUInt>();
			info.msg.simpleUint.value = customColour;
		}
	}

	public override void Load(LoadInfo info)
	{
		base.Load(info);
		customColour = 0u;
		if (info.msg.simpleUint != null)
		{
			customColour = info.msg.simpleUint.value;
		}
		if (info.msg.buildingBlock != null)
		{
			wallpaperID = info.msg.buildingBlock.wallpaperID;
			wallpaperID2 = info.msg.buildingBlock.wallpaperID2;
			wallpaperHealth = info.msg.buildingBlock.wallpaperHealth;
			wallpaperHealth2 = info.msg.buildingBlock.wallpaperHealth2;
			SetConditionalModel(info.msg.buildingBlock.model);
			SetGrade((BuildingGrade.Enum)info.msg.buildingBlock.grade);
		}
		if (info.fromDisk)
		{
			SetFlag(Flags.Reserved1, b: false);
			UpdateSkin();
		}
	}

	public override void AttachToBuilding(DecayEntity other)
	{
		if (other != null && other is BuildingBlock)
		{
			AttachToBuilding(other.buildingID);
			BuildingManager.server.CheckMerge(this);
		}
		else
		{
			AttachToBuilding(BuildingManager.server.NewBuildingID());
		}
	}

	public override void ServerInit()
	{
		blockDefinition = PrefabAttribute.server.Find<Construction>(prefabID);
		if (blockDefinition == null)
		{
			Debug.LogError("Couldn't find Construction for prefab " + prefabID);
		}
		base.ServerInit();
		UpdateSkin();
		if (HasFlag(Flags.Reserved1) || !Rust.Application.isLoadingSave)
		{
			StartBeingRotatable();
		}
		if (!CullBushes || Rust.Application.isLoadingSave)
		{
			return;
		}
		List<BushEntity> obj = Facepunch.Pool.Get<List<BushEntity>>();
		Vis.Entities(WorldSpaceBounds(), obj, 67108864);
		foreach (BushEntity item in obj)
		{
			if (item.isServer)
			{
				item.Kill();
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public override void Hurt(HitInfo info)
	{
		if (ConVar.Server.pve && (bool)info.Initiator && info.Initiator is BasePlayer)
		{
			(info.Initiator as BasePlayer).Hurt(info.damageTypes.Total(), DamageType.Generic);
		}
		else
		{
			if ((bool)info.Initiator && info.Initiator is BasePlayer { IsInTutorial: not false })
			{
				return;
			}
			if (HasWallpaper())
			{
				DamageType majorityDamageType = info.damageTypes.GetMajorityDamageType();
				bool flag = info.damageTypes.Contains(DamageType.Explosion);
				DamageTypeList damageTypeList = info.damageTypes.Clone();
				if (wallpaperProtection != null)
				{
					wallpaperProtection.Scale(damageTypeList);
				}
				float totalDamage = damageTypeList.Total();
				if (majorityDamageType == DamageType.Decay || flag || majorityDamageType == DamageType.Heat)
				{
					DamageWallpaper(totalDamage);
					DamageWallpaper(totalDamage, 1);
				}
				else
				{
					bool flag2 = false;
					for (int i = 0; i < propDirection.Length; i++)
					{
						if (propDirection[i].IsWeakspot(base.transform, info))
						{
							flag2 = true;
							break;
						}
					}
					DamageWallpaper(totalDamage, (!flag2) ? 1 : 0);
				}
			}
			base.Hurt(info);
		}
	}

	public bool HasWallpaper()
	{
		if (!(wallpaperHealth > 0f))
		{
			return wallpaperHealth2 > 0f;
		}
		return true;
	}

	public bool HasWallpaper(int side)
	{
		if (side != 0)
		{
			return wallpaperHealth2 > 0f;
		}
		return wallpaperHealth > 0f;
	}

	public override bool IsOccupied(Socket_Base socket)
	{
		if (socket is Socket_Specific_Female socket_Specific_Female && socket_Specific_Female.socketName.Contains("wallpaper"))
		{
			int side = ((!socket.socketName.EndsWith("1")) ? 1 : 0);
			return HasWallpaper(side);
		}
		return base.IsOccupied(socket);
	}

	public void SetWallpaper(ulong id, int side = 0)
	{
		if (side == 0)
		{
			if (HasWallpaper(side) && wallpaperID == id)
			{
				return;
			}
			wallpaperID = id;
			wallpaperHealth = 100f;
		}
		else
		{
			if (HasWallpaper(side) && wallpaperID2 == id)
			{
				return;
			}
			wallpaperID2 = id;
			wallpaperHealth2 = 100f;
		}
		if (base.isServer)
		{
			SetConditionalModel(currentSkin.DetermineConditionalModelState(this));
			SendNetworkUpdateImmediate();
			ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
		}
	}

	public void RemoveWallpaper(int side)
	{
		switch (side)
		{
		case 0:
			wallpaperHealth = -1f;
			wallpaperID = 0uL;
			break;
		case 1:
			wallpaperHealth2 = -1f;
			wallpaperID2 = 0uL;
			break;
		}
		if (base.isServer)
		{
			SetConditionalModel(currentSkin.DetermineConditionalModelState(this));
			SendNetworkUpdateImmediate();
			ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
		}
	}

	[RPC_Server]
	[RPC_Server.MaxDistance(3f)]
	private void RPC_PickupWallpaperStart(RPCMessage msg)
	{
		if (msg.player.CanInteract() && CanPickup(msg.player))
		{
			bool flag = msg.read.Bool();
			if (HasWallpaper((!flag) ? 1 : 0))
			{
				Item item = ItemManager.Create(WallpaperPlanner.WallpaperItemDef, 1, flag ? wallpaperID : wallpaperID2);
				msg.player.GiveItem(item, GiveItemReason.PickedUp);
				RemoveWallpaper((!flag) ? 1 : 0);
			}
		}
	}

	private void DamageWallpaper(float totalDamage, int side = 0)
	{
		switch (side)
		{
		case 0:
			wallpaperHealth -= totalDamage;
			if (wallpaperHealth <= 0f)
			{
				RemoveWallpaper(0);
			}
			break;
		case 1:
			wallpaperHealth2 -= totalDamage;
			if (wallpaperHealth2 <= 0f)
			{
				RemoveWallpaper(1);
			}
			break;
		}
	}

	public override void StabilityCheck()
	{
		base.StabilityCheck();
		if (HasWallpaper(1))
		{
			Invoke(CheckWallpaper, 0.5f);
		}
	}

	public override void OnDecay(Decay decay, float decayDeltaTime)
	{
		base.OnDecay(decay, decayDeltaTime);
		if (HasWallpaper(1))
		{
			CheckWallpaper();
		}
	}

	public void CheckWallpaper()
	{
		Construction construction = WallpaperPlanner.Settings?.GetConstruction(this);
		if (!(construction == null) && SocketMod_Inside.IsOutside(base.transform.position + construction.deployOffset.localPosition + base.transform.right * 0.2f, base.transform))
		{
			RemoveWallpaper(1);
		}
	}

	public bool CanSeeWallpaperSocket(BasePlayer player, int side = 0)
	{
		Construction construction = WallpaperPlanner.Settings?.GetConstruction(this);
		if (construction == null)
		{
			return false;
		}
		Vector3 position = base.transform.position;
		Vector3 vector = construction.deployOffset?.localPosition ?? Vector3.zero;
		if (side == 1)
		{
			vector.x = 0f - vector.x;
		}
		Vector3 vector2 = position + base.transform.rotation * vector - player.eyes.HeadRay().origin;
		List<RaycastHit> obj = Facepunch.Pool.Get<List<RaycastHit>>();
		GamePhysics.TraceAll(new Ray(player.eyes.HeadRay().origin, vector2.normalized), 0f, obj, vector2.magnitude, 2097152, QueryTriggerInteraction.Ignore);
		bool result = true;
		foreach (RaycastHit item in obj)
		{
			BaseEntity baseEntity = item.transform.ToBaseEntity();
			if (!(baseEntity == null) && baseEntity == this)
			{
				result = false;
			}
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
		return result;
	}

	public override bool CanPickup(BasePlayer player)
	{
		if (!HasWallpaper())
		{
			return false;
		}
		if (player.IsHoldingEntity<Hammer>() && player.CanBuild())
		{
			if (!HasWallpaper(0) || !CanSeeWallpaperSocket(player))
			{
				if (HasWallpaper(1))
				{
					return CanSeeWallpaperSocket(player, 1);
				}
				return false;
			}
			return true;
		}
		return false;
	}
}
