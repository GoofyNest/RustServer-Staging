using UnityEngine.EventSystems;

public class FpStandaloneInputModule : StandaloneInputModule
{
	public PointerEventData CurrentData => m_PointerData[-1];
}
