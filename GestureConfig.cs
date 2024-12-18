using ConVar;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(menuName = "Rust/Gestures/Gesture Config")]
public class GestureConfig : ScriptableObject
{
	public enum GestureType
	{
		Player,
		NPC,
		Cinematic
	}

	public enum PlayerModelLayer
	{
		UpperBody = 3,
		FullBody
	}

	public enum MovementCapabilities
	{
		FullMovement,
		NoMovement
	}

	public enum AnimationType
	{
		OneShot,
		Loop
	}

	public enum GestureActionType
	{
		None,
		ShowNameTag,
		DanceAchievement,
		Surrender,
		RockPaperScissors
	}

	[ReadOnly]
	public uint gestureId;

	public string gestureCommand;

	public string convarName;

	public Translate.Phrase gestureName;

	public Translate.Phrase gestureDescription;

	public Sprite icon;

	public AnimationType animationType;

	public float duration = 1.5f;

	public bool canCancel = true;

	public MovementCapabilities movementMode;

	public BasePlayer.CameraMode viewMode;

	public bool hideInWheel;

	public VideoClip previewClip;

	[Header("Player model setup")]
	public PlayerModelLayer playerModelLayer = PlayerModelLayer.UpperBody;

	public GestureType gestureType;

	public bool hideHeldEntity = true;

	public bool canDuckDuringGesture;

	public bool hasViewmodelAnimation = true;

	public float viewmodelHolsterDelay;

	public bool useRootMotion;

	public bool forceForwardRotation;

	[Header("Interaction")]
	public bool hasMultiplayerInteraction;

	public Translate.Phrase joinPlayerPhrase = new Translate.Phrase();

	public Translate.Phrase joinPlayerDescPhrase = new Translate.Phrase();

	[Header("Ownership")]
	public GestureActionType actionType;

	public bool forceUnlock;

	public SteamDLCItem dlcItem;

	public SteamInventoryItem inventoryItem;

	public bool IsOwnedBy(BasePlayer player, bool allowCinematic = false)
	{
		if (forceUnlock)
		{
			return true;
		}
		if (gestureType == GestureType.NPC)
		{
			if (player != null)
			{
				return player.IsNpc;
			}
			return false;
		}
		if (gestureType == GestureType.Cinematic)
		{
			if (!allowCinematic && (!(player != null) || !player.IsAdmin))
			{
				return Server.cinematic;
			}
			return true;
		}
		if (dlcItem != null && player != null)
		{
			return dlcItem.CanUse(player);
		}
		if (inventoryItem != null && player != null && player.blueprints.steamInventory.HasItem(inventoryItem.id))
		{
			return true;
		}
		return false;
	}

	public bool CanBeUsedBy(BasePlayer player)
	{
		if (player.isMounted)
		{
			if (playerModelLayer == PlayerModelLayer.FullBody)
			{
				return false;
			}
			if (player.GetMounted().allowedGestures == BaseMountable.MountGestureType.None)
			{
				return false;
			}
		}
		if (player.IsSwimming() && playerModelLayer == PlayerModelLayer.FullBody)
		{
			return false;
		}
		if (playerModelLayer == PlayerModelLayer.FullBody && player.modelState.ducked)
		{
			return false;
		}
		return true;
	}
}
