using UnityEngine;

namespace FIMSpace.Basics;

public class FBasic_DuplicateObjects : MonoBehaviour
{
	public enum FEDuplicateDirection
	{
		GoIterative,
		GoFromCenter
	}

	public enum FEDuplicateOrigin
	{
		FromToDuplicate,
		FromComponent
	}

	[Tooltip("Put here object which you want duplicate")]
	public GameObject ToDuplicate;

	[Tooltip("How many copies in which axis")]
	public Vector3 DuplicatesCount = new Vector3(3f, 1f, 3f);

	[Tooltip("How far from each other should be created copies")]
	public Vector3 Offsets = new Vector3(3f, 0f, 3f);

	public Vector3 Randomize = new Vector3(0f, 0f, 0f);

	public Vector3 RandomRotate = new Vector3(0f, 0f, 0f);

	public Vector3 RandomScale = new Vector3(0f, 0f, 0f);

	public int Seed;

	[Tooltip("If you want raycast from up and put objects for example on terrain")]
	public bool PlaceOnGround;

	[Tooltip("Duplicates will be created when entered playmode")]
	public bool DuplicateAtStart;

	public float GizmosSize = 1f;

	public FEDuplicateDirection DuplicationType;

	public FEDuplicateOrigin DuplicationOrigin;

	private void Start()
	{
		if (DuplicateAtStart)
		{
			Duplicate();
		}
	}

	private void Reset()
	{
		Seed = Random.Range(-2147483646, 2147483646);
	}

	public void Duplicate()
	{
		if (ToDuplicate == null)
		{
			return;
		}
		Random.InitState(Seed);
		Vector3 vector = ((DuplicationOrigin != FEDuplicateOrigin.FromComponent) ? ToDuplicate.transform.position : base.transform.position);
		if (DuplicationType == FEDuplicateDirection.GoIterative)
		{
			for (int i = 0; (float)i < DuplicatesCount.x; i++)
			{
				for (int j = 0; (float)j < DuplicatesCount.y; j++)
				{
					for (int k = 0; (float)k < DuplicatesCount.z; k++)
					{
						if (DuplicationOrigin == FEDuplicateOrigin.FromToDuplicate && i == 0 && j == 0 && k == 0)
						{
							continue;
						}
						Vector3 vector2 = vector;
						vector2.x += (float)i * Offsets.x;
						vector2.y += (float)j * Offsets.y;
						vector2.z += (float)k * Offsets.z;
						GameObject gameObject = Object.Instantiate(ToDuplicate);
						gameObject.transform.position = vector2 + GetRandomVector();
						gameObject.transform.rotation *= Quaternion.Euler(Random.Range(0f - RandomRotate.x, RandomRotate.x), Random.Range(0f - RandomRotate.y, RandomRotate.y), Random.Range(0f - RandomRotate.z, RandomRotate.z));
						Vector3 localScale = gameObject.transform.localScale + new Vector3(Random.Range(0f - RandomScale.x, RandomScale.x), Random.Range(0f - RandomScale.y, RandomScale.y), Random.Range(0f - RandomScale.z, RandomScale.z));
						gameObject.transform.localScale = localScale;
						if (PlaceOnGround)
						{
							Physics.Raycast(gameObject.transform.position + Vector3.up * 100f, Vector3.down, out var hitInfo, 200f);
							if ((bool)hitInfo.transform)
							{
								gameObject.transform.position = hitInfo.point;
							}
						}
					}
				}
			}
		}
		else
		{
			if (DuplicationType != FEDuplicateDirection.GoFromCenter)
			{
				return;
			}
			for (int l = 0; (float)l < DuplicatesCount.x; l++)
			{
				for (int m = 0; (float)m < DuplicatesCount.y; m++)
				{
					for (int n = 0; (float)n < DuplicatesCount.z; n++)
					{
						float num = 1f;
						float num2 = 1f;
						float num3 = 1f;
						if (l % 2 == 1)
						{
							num = -1f;
						}
						if (m % 2 == 1)
						{
							num2 = -1f;
						}
						if (n % 2 == 1)
						{
							num3 = -1f;
						}
						Vector3 vector3 = new Vector3(l, m, n);
						if (l == 0)
						{
							vector3.x = 0.5f;
						}
						if (m == 0)
						{
							vector3.y = 0.5f;
						}
						if (n == 0)
						{
							vector3.z = 0.5f;
						}
						Vector3 vector4 = vector;
						vector4.x += vector3.x * Offsets.x * num;
						vector4.y += vector3.y * Offsets.y * num2;
						vector4.z += vector3.z * Offsets.z * num3;
						GameObject gameObject2 = Object.Instantiate(ToDuplicate);
						gameObject2.transform.position = vector4 + GetRandomVector();
						gameObject2.transform.rotation *= Quaternion.Euler(Random.Range(0f - RandomRotate.x, RandomRotate.x), Random.Range(0f - RandomRotate.y, RandomRotate.y), Random.Range(0f - RandomRotate.z, RandomRotate.z));
						Vector3 localScale2 = gameObject2.transform.localScale + new Vector3(Random.Range(0f - RandomScale.x, RandomScale.x), Random.Range(0f - RandomScale.y, RandomScale.y), Random.Range(0f - RandomScale.z, RandomScale.z));
						gameObject2.transform.localScale = localScale2;
						if (PlaceOnGround)
						{
							Physics.Raycast(gameObject2.transform.position + Vector3.up * 100f, Vector3.down, out var hitInfo2, 200f);
							if ((bool)hitInfo2.transform)
							{
								gameObject2.transform.position = hitInfo2.point;
							}
						}
					}
				}
			}
		}
	}

	private void OnDrawGizmos()
	{
		if (ToDuplicate == null)
		{
			return;
		}
		Random.InitState(Seed);
		Vector3 vector = ((DuplicationOrigin != FEDuplicateOrigin.FromComponent) ? ToDuplicate.transform.position : base.transform.position);
		Gizmos.color = new Color(0.2f, 0.7f, 0.2f, 0.6f);
		if (DuplicationType == FEDuplicateDirection.GoIterative)
		{
			for (int i = 0; (float)i < DuplicatesCount.x; i++)
			{
				for (int j = 0; (float)j < DuplicatesCount.y; j++)
				{
					for (int k = 0; (float)k < DuplicatesCount.z; k++)
					{
						Vector3 vector2 = vector;
						vector2.x += (float)i * Offsets.x;
						vector2.y += (float)j * Offsets.y;
						vector2.z += (float)k * Offsets.z;
						Gizmos.DrawCube(vector2 + GetRandomVector(), Vector3.one * 0.25f * GizmosSize);
					}
				}
			}
		}
		else
		{
			if (DuplicationType != FEDuplicateDirection.GoFromCenter)
			{
				return;
			}
			for (int l = 0; (float)l < DuplicatesCount.x; l++)
			{
				for (int m = 0; (float)m < DuplicatesCount.y; m++)
				{
					for (int n = 0; (float)n < DuplicatesCount.z; n++)
					{
						float num = 1f;
						float num2 = 1f;
						float num3 = 1f;
						if (l % 2 == 1)
						{
							num = -1f;
						}
						if (m % 2 == 1)
						{
							num2 = -1f;
						}
						if (n % 2 == 1)
						{
							num3 = -1f;
						}
						Vector3 vector3 = new Vector3(l, m, n);
						if (l == 0)
						{
							vector3.x = 0.5f;
						}
						if (m == 0)
						{
							vector3.y = 0.5f;
						}
						if (n == 0)
						{
							vector3.z = 0.5f;
						}
						Vector3 vector4 = vector;
						vector4.x += vector3.x * Offsets.x * num;
						vector4.y += vector3.y * Offsets.y * num2;
						vector4.z += vector3.z * Offsets.z * num3;
						Gizmos.DrawCube(vector4 + GetRandomVector(), Vector3.one * 0.25f * GizmosSize);
					}
				}
			}
		}
	}

	private Vector3 GetRandomVector()
	{
		if (Randomize == Vector3.zero)
		{
			return Randomize;
		}
		return new Vector3(Random.Range(0f - Randomize.x, Randomize.x), Random.Range(0f - Randomize.y, Randomize.y), Random.Range(0f - Randomize.z, Randomize.z));
	}
}
