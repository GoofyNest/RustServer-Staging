using System;
using System.Collections.Generic;
using Facepunch;
using Rust.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TweakUIDropdown : TweakUIBase
{
	[Serializable]
	public class NameValue
	{
		public string value;

		public Color imageColor;

		public Translate.Phrase label;

		public bool rightToLeft;

		public bool useColorInsteadOfText;
	}

	public RustText Current;

	public Image BackgroundImage;

	public Image CurrentColor;

	public RustButton Opener;

	public RectTransform Dropdown;

	public RectTransform DropdownContainer;

	public GameObject DropdownItemPrefab;

	public NameValue[] nameValues;

	public bool assignImageColor;

	public bool forceEnglish;

	public UnityEvent onValueChanged = new UnityEvent();

	public int currentValue;

	protected override void Init()
	{
		base.Init();
		DropdownItemPrefab.SetActive(value: false);
		Dropdown.gameObject.SetActive(value: true);
		UpdateDropdownOptions();
		Opener.SetToggleFalse();
		ResetToConvar();
	}

	protected void OnEnable()
	{
		ResetToConvar();
	}

	public void UpdateDropdownOptions()
	{
		List<RustButton> obj = Pool.Get<List<RustButton>>();
		DropdownContainer.GetComponentsInChildren(includeInactive: false, obj);
		foreach (RustButton item in obj)
		{
			UnityEngine.Object.Destroy(item.gameObject);
		}
		Pool.FreeUnmanaged(ref obj);
		for (int i = 0; i < nameValues.Length; i++)
		{
			GameObject obj2 = UnityEngine.Object.Instantiate(DropdownItemPrefab, DropdownContainer);
			int itemIndex = i;
			RustButton component = obj2.GetComponent<RustButton>();
			NameValue nameValue = nameValues[i];
			if (forceEnglish)
			{
				component.Text.SetText(nameValue.label.english, localized: false, nameValue.rightToLeft);
			}
			else
			{
				component.Text.SetPhrase(nameValue.label);
			}
			TweakUIItem component2 = obj2.GetComponent<TweakUIItem>();
			if (component2 != null && component2.Image != null && component2.Text != null)
			{
				component2.Text.gameObject.SetActive(!nameValue.useColorInsteadOfText);
				component2.Image.gameObject.SetActive(nameValue.useColorInsteadOfText);
				component2.Image.color = nameValue.imageColor;
			}
			component.OnReleased.AddListener(delegate
			{
				ChangeValue(itemIndex);
			});
			obj2.SetActive(value: true);
		}
	}

	public void OnValueChanged()
	{
		if (ApplyImmediatelyOnChange)
		{
			SetConvarValue();
		}
	}

	public void OnDropdownOpen()
	{
		RectTransform rectTransform = (RectTransform)base.transform;
		if (rectTransform.position.y <= (float)Screen.height / 2f)
		{
			Dropdown.pivot = new Vector2(0.5f, 0f);
			Dropdown.anchoredPosition = Dropdown.anchoredPosition.WithY(0f);
		}
		else
		{
			Dropdown.pivot = new Vector2(0.5f, 1f);
			Dropdown.anchoredPosition = Dropdown.anchoredPosition.WithY(0f - rectTransform.rect.height);
		}
	}

	public void ChangeValue(int index)
	{
		Opener.SetToggleFalse();
		int num = Mathf.Clamp(index, 0, nameValues.Length - 1);
		bool num2 = num != currentValue;
		currentValue = num;
		if (ApplyImmediatelyOnChange)
		{
			SetConvarValue();
		}
		else
		{
			ShowValue(nameValues[currentValue].value);
		}
		if (num2)
		{
			onValueChanged?.Invoke();
		}
	}

	protected override void SetConvarValue()
	{
		base.SetConvarValue();
		NameValue nameValue = nameValues[currentValue];
		if (conVar != null && !(conVar.String == nameValue.value))
		{
			conVar.Set(nameValue.value);
		}
	}

	public override void ResetToConvar()
	{
		base.ResetToConvar();
		if (conVar != null)
		{
			string @string = conVar.String;
			ShowValue(@string);
		}
	}

	protected void ShowValue(string value)
	{
		for (int i = 0; i < nameValues.Length; i++)
		{
			NameValue nameValue = nameValues[i];
			if (nameValue.value != value)
			{
				continue;
			}
			Current.enabled = !nameValue.useColorInsteadOfText;
			Current.SetPhrase(nameValue.label);
			currentValue = i;
			if (assignImageColor)
			{
				BackgroundImage.color = nameValue.imageColor;
			}
			if (CurrentColor != null)
			{
				if (nameValue.useColorInsteadOfText)
				{
					CurrentColor.color = nameValue.imageColor;
					CurrentColor.enabled = true;
				}
				else
				{
					CurrentColor.enabled = false;
				}
			}
			break;
		}
	}
}
