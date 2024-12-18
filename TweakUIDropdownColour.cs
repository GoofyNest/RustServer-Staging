using Rust.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TweakUIDropdownColour : TweakUIBase
{
	public Image BackgroundImage;

	public RustButton Opener;

	public RectTransform Dropdown;

	public RectTransform DropdownContainer;

	public GameObject DropdownItemPrefab;

	public AccessibilityColourCollection forColourCollection;

	public AccessibilityMaterialCollection forMaterialCollection;

	public UnityEvent onValueChanged = new UnityEvent();

	public int currentValue;
}
