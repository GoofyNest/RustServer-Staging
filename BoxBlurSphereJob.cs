using Facepunch.MarchingCubes;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
internal struct BoxBlurSphereJob : IJob
{
	public Point3DGrid Grid;

	public int3 Origin;

	public int R2;

	public void Execute()
	{
		for (int i = -R2; i <= R2; i++)
		{
			for (int j = -R2; j <= R2; j++)
			{
				for (int k = -R2; k <= R2; k++)
				{
					int3 @int = new int3(i, j, k);
					int3 int2 = Origin + @int;
					if (math.distancesq(int2, Origin) > (float)R2 || !Grid.InBoundsNotTouching(int2))
					{
						continue;
					}
					float num = 0f;
					int num2 = 0;
					for (int l = -1; l <= 1; l++)
					{
						for (int m = -1; m <= 1; m++)
						{
							for (int n = -1; n <= 1; n++)
							{
								if (l != 0 || m != 0 || n != 0)
								{
									int3 int3 = int2 + new int3(l, m, n);
									if (Grid.InBounds(int3))
									{
										num += Grid.Sample(int3);
										num2++;
									}
								}
							}
						}
					}
					Grid[int2] = num / (float)num2 > 0.5f;
				}
			}
		}
	}
}
