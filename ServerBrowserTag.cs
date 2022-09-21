using Rust.UI;
using UnityEngine;

public class ServerBrowserTag : MonoBehaviour
{
	public string serverTag;

	public string[] serverHasAnyOf;

	public string[] serverHasNoneOf;

	public RustButton button;

	public bool IsActive
	{
		get
		{
			if (button != null)
			{
				return button.IsPressed;
			}
			return false;
		}
	}

	[ContextMenu("Upgrade")]
	public void UpgraddeValue()
	{
		if (serverHasAnyOf == null || serverHasAnyOf.Length != 1)
		{
			Debug.Log("Cannot upgrade " + base.name, this);
		}
		else if (string.IsNullOrWhiteSpace(serverTag))
		{
			serverTag = serverHasAnyOf[0];
		}
	}
}
