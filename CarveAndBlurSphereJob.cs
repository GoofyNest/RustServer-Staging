using Facepunch.MarchingCubes;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
internal struct CarveAndBlurSphereJob : IJob
{
	public Point3DGrid Grid;

	public int3 Origin;

	public int R2;

	public int R2B;

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
					if (!(math.distancesq(int2, Origin) > (float)R2) && Grid.InBounds(int2))
					{
						Grid[int2] = false;
					}
				}
			}
		}
		for (int l = -R2B; l <= R2B; l++)
		{
			for (int m = -R2B; m <= R2B; m++)
			{
				for (int n = -R2B; n <= R2B; n++)
				{
					int3 int3 = new int3(l, m, n);
					int3 int4 = Origin + int3;
					if (math.distancesq(int4, Origin) > (float)R2 || !Grid.InBoundsNotTouching(int4))
					{
						continue;
					}
					float num = 0f;
					int num2 = 0;
					for (int num3 = -1; num3 <= 1; num3++)
					{
						for (int num4 = -1; num4 <= 1; num4++)
						{
							for (int num5 = -1; num5 <= 1; num5++)
							{
								if (num3 != 0 || num4 != 0 || num5 != 0)
								{
									int3 int5 = int4 + new int3(num3, num4, num5);
									if (Grid.InBounds(int5))
									{
										num += Grid.Sample(int5);
										num2++;
									}
								}
							}
						}
					}
					Grid[int4] = num / (float)num2 > 0.5f;
				}
			}
		}
	}
}
