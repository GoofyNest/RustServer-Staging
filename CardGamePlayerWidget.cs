using Rust.UI;
using UnityEngine;
using UnityEngine.UI;

public class CardGamePlayerWidget : MonoBehaviour
{
	private enum BetDisplayType
	{
		BetThisTurn,
		BetThisRound
	}

	[SerializeField]
	private GameObjectRef cardImageSmallPrefab;

	[SerializeField]
	private GameObjectRef cardImageSmallerPrefab;

	[SerializeField]
	private RawImage avatar;

	[SerializeField]
	private RustText playerName;

	[SerializeField]
	private RustText scrapTotal;

	[SerializeField]
	private RustText betTotal;

	[SerializeField]
	private Image background;

	[SerializeField]
	private Color inactiveBackground;

	[SerializeField]
	private Color activeBackground;

	[SerializeField]
	private Color foldedBackground;

	[SerializeField]
	private Color winnerBackground;

	[SerializeField]
	private Animation actionShowAnimation;

	[SerializeField]
	private RustText actionText;

	[SerializeField]
	private Sprite noIcon;

	[SerializeField]
	private Sprite canSeeIcon;

	[SerializeField]
	private Sprite cannotSeeIcon;

	[SerializeField]
	private Image cornerIcon;

	[SerializeField]
	private Transform cardDisplayParent;

	[SerializeField]
	private BetDisplayType betDisplayType;

	[SerializeField]
	private GameObject circle;

	[SerializeField]
	private RustText circleText;
}
