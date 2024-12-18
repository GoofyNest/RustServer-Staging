using System;
using Rust.UI;
using UnityEngine;

public class BuildingPrivilegeRetroScreen : FacepunchBehaviour, INotifyLOD, IClientComponent
{
	[Serializable]
	public struct Screen
	{
		public CanvasGroup group;

		public CanvasGroup onGroup;

		public CanvasGroup offGroup;

		public void TurnOnOff(bool on)
		{
			onGroup.gameObject.SetActive(on);
			onGroup.alpha = (on ? 1 : 0);
			offGroup.gameObject.SetActive(!on);
			offGroup.alpha = ((!on) ? 1 : 0);
		}
	}

	[SerializeField]
	private CanvasGroup screenCanvas;

	[Space]
	[Header("PROTECTED TIME")]
	public RustText protectedTimeText;

	public int decayWarningThreshold = 130;

	public GameObject decayWarningGroup;

	public GameObject decayingGroup;

	[Space]
	[Header("UPKEEP")]
	public VirtualItemIcon[] costIcons;

	public RustText[] paginationTexts;

	[Space]
	[Header("BLOCKS")]
	public GameObject[] blocksType;

	public RustText blockCountText;

	public RustText doorCountText;

	[Space]
	public Renderer screenRenderer;

	[ColorUsage(true, true)]
	public Color fromScreenEmissionColor;

	[ColorUsage(true, true)]
	public Color screenEmissionColor;

	public AnimationCurve tweenCurve;

	public float animDuration = 0.7f;

	public Animation screensAnim;

	public Screen[] screens;
}
