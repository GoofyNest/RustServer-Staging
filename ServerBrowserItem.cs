using Rust.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServerBrowserItem : MonoBehaviour
{
	public TextMeshProUGUI serverName;

	public RustFlexText mapName;

	public TextMeshProUGUI playerCount;

	public TextMeshProUGUI ping;

	public TextMeshProUGUI pendingPing;

	public Toggle favourited;

	public ServerBrowserTagList serverTagList;

	public Color pingColor;

	public Color pingWarningColor;

	public Tooltip pingWarningTooltip;
}
