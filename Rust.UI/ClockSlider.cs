using UnityEngine;

namespace Rust.UI;

public class ClockSlider : RustSlider
{
	private bool isUpdatingText;

	public override float Value
	{
		get
		{
			return base.Value;
		}
		set
		{
			value = Mathf.Clamp(value, MinValue, MaxValue);
			if (Integer)
			{
				value = Mathf.Round(value);
			}
			if (ValueInternal != value)
			{
				ValueInternal = value;
			}
			string text = FormatText(value);
			if (NumberInput != null && !NumberInput.IsFocused && NumberInput.Text != text)
			{
				UpdateTextNoNotify(text);
			}
			SliderCanvas.fillAmount = base.ValueNormalized;
			if (lastCallbackValue != value)
			{
				lastCallbackValue = value;
				OnChanged?.Invoke(value);
			}
		}
	}

	protected override void Awake()
	{
		base.Awake();
		if (NumberInput != null)
		{
			NumberInput.OnValueChanged.RemoveListener(TextChanged);
			NumberInput.OnValueChanged.AddListener(TextChanged);
			NumberInput.OnEndEdit.RemoveListener(OnEndEdit);
			NumberInput.OnEndEdit.AddListener(OnEndEdit);
		}
	}

	public void OnEndEdit(string text)
	{
		if (isUpdatingText)
		{
			return;
		}
		if (!text.Contains(":"))
		{
			text = ((text.Length == 4) ? text.Insert(2, ":") : ((text.Length == 3) ? text.Insert(1, ":") : ((text.Length != 1) ? "00:00" : ("0" + text + ":00"))));
			UpdateTextNoNotify(text);
		}
		else
		{
			if (text.Length == 3)
			{
				text = text.Insert(3, "00");
			}
			UpdateTextNoNotify(text);
		}
		UpdateValue(text, updateText: true);
	}

	public new void TextChanged(string text)
	{
		if (isUpdatingText)
		{
			return;
		}
		int num = -1;
		if (!text.Contains(":"))
		{
			if (text.Length == 2)
			{
				text = text.Insert(2, ":");
				num = 3;
			}
			if (text.Length == 1)
			{
				int.TryParse(text, out var result);
				if (result > 2)
				{
					text = "0" + text + ":";
					num = 3;
				}
			}
			UpdateTextNoNotify(text);
			if (num != -1)
			{
				NumberInput.InputField.caretPosition = num;
			}
		}
		UpdateValue(text, updateText: false);
	}

	private void UpdateValue(string text, bool updateText)
	{
		string[] array = text.Split(':');
		if (array.Length != 2 || !int.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2))
		{
			return;
		}
		result = Mathf.Clamp(result, 0, 23);
		result2 = Mathf.Clamp(result2, 0, 59);
		Value = (float)result + (float)result2 / 60f;
		if (updateText)
		{
			string text2 = FormatText(Value);
			if (NumberInput.Text != text2)
			{
				UpdateTextNoNotify(text2);
			}
		}
	}

	private string FormatText(float value)
	{
		int num = Mathf.RoundToInt(value * 60f);
		int num2 = num / 60;
		int num3 = num % 60;
		return $"{num2:D2}:{num3:D2}";
	}

	private void UpdateTextNoNotify(string text)
	{
		isUpdatingText = true;
		NumberInput.Text = text;
		isUpdatingText = false;
	}
}
