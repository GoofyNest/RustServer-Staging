using Rust;
using UnityEngine;

public class SocketMod_Inside : SocketMod
{
	public bool wantsInside = true;

	private static readonly Vector3[] outsideLookupDirs = new Vector3[8]
	{
		new Vector3(1f, 1f, 0f).normalized,
		new Vector3(0f, -1f, 0f).normalized,
		new Vector3(0f, 1f, 1f).normalized,
		new Vector3(0f, 1f, -1f).normalized,
		new Vector3(1f, 0f, 0f).normalized,
		new Vector3(0f, 1f, 0f).normalized,
		new Vector3(0.5f, 0f, 1f).normalized,
		new Vector3(0.5f, 0f, -1f).normalized
	};

	protected override Translate.Phrase ErrorPhrase
	{
		get
		{
			if (!wantsInside)
			{
				return ConstructionErrors.WantsOutside;
			}
			return ConstructionErrors.WantsInside;
		}
	}

	public override bool DoCheck(Construction.Placement place)
	{
		bool flag = IsOutside(place.transform.position + baseSocket.localPosition + place.transform.right * 0.2f, place.transform);
		return !wantsInside == flag;
	}

	public static bool IsOutside(Vector3 pos, Transform tr, int layerMask = 2162688)
	{
		float num = 20f;
		int num2 = 0;
		bool flag = true;
		for (int i = 0; i < outsideLookupDirs.Length; i++)
		{
			Vector3 direction = tr.TransformDirection(outsideLookupDirs[i]);
			if (Physics.Raycast(new Ray(pos, direction), out var hitInfo, num - 0.5f, layerMask))
			{
				if (hitInfo.collider.gameObject.IsOnLayer(Layer.Construction))
				{
					num2++;
				}
			}
			else
			{
				flag = false;
			}
		}
		if (flag)
		{
			return num2 < 2;
		}
		return true;
	}
}
