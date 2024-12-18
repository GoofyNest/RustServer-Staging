using Rust.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VLB;

public class UIMixingTableItem : MonoBehaviour
{
	public Image ItemIcon;

	public Tooltip ItemTooltip;

	public RustText TextItemNameAndQuantity;

	public UIMixingTableItemIngredient[] Ingredients;

	public Recipe Recipe;

	public void Init(Recipe r, UnityAction<Recipe> onClicked)
	{
		Recipe = r;
		if (Recipe == null)
		{
			return;
		}
		base.gameObject.GetOrAddComponent<Button>().onClick.AddListener(delegate
		{
			onClicked(Recipe);
		});
		ItemIcon.sprite = Recipe.DisplayIcon;
		TextItemNameAndQuantity.SetText($"{Recipe.ProducedItemCount} x {Recipe.DisplayName}", localized: true);
		ItemTooltip.Text = Recipe.DisplayDescription;
		for (int i = 0; i < Ingredients.Length; i++)
		{
			if (i >= Recipe.Ingredients.Length)
			{
				Ingredients[i].InitBlank();
			}
			else
			{
				Ingredients[i].Init(Recipe.Ingredients[i]);
			}
		}
	}

	public void CleanUp()
	{
		Button component = base.gameObject.GetComponent<Button>();
		if (component != null)
		{
			component.onClick.RemoveAllListeners();
		}
	}

	public void SetAvailable(bool flag)
	{
		TextItemNameAndQuantity.color = (flag ? new Color(0.78f, 0.78f, 0.78f) : Color.grey);
	}
}
