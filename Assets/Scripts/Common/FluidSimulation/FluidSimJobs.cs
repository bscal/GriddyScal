using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Common.FluidSimulation
{

    // *****************************
    // LinSolve Job
    // *****************************

    public struct LinearJob : IJob
    {
        [ReadOnly] public int N;
        [ReadOnly] public float dt;
        [ReadOnly] public float diff;
        [ReadOnly] public int b;
        [ReadOnly] public int iters;
        public NativeArray<float> x;
        public NativeArray<float> x0;

        public void Execute()
        {
            float a = dt * diff * (N - 2) * (N - 2);
            Lin_solve(b, a, 1 + 4 * a);
        }


        void Lin_solve(int b, float a, float c)
        {
            float cRecip = 1.0f / c;
            for (int k = 0; k < iters; k++)
            {
                for (int j = 1; j < N - 1; j++)
                {
                    for (int i = 1; i < N - 1; i++)
                    {
                        x[IX(i, j)] =
                          (x0[IX(i, j)]
                          + a * (x[IX(i + 1, j)]
                          + x[IX(i - 1, j)]
                          + x[IX(i, j + 1)]
                          + x[IX(i, j - 1)]
                          )) * cRecip;
                    }
                }

                Set_bnd(b);
            }
        }

        void Set_bnd(int b)
        {
            for (int i = 1; i < N - 1; i++)
            {
                x[IX(i, 0)] = b == 2 ? -x[IX(i, 1)] : x[IX(i, 1)];
                x[IX(i, N - 1)] = b == 2 ? -x[IX(i, N - 2)] : x[IX(i, N - 2)];
            }
            for (int j = 1; j < N - 1; j++)
            {
                x[IX(0, j)] = b == 1 ? -x[IX(1, j)] : x[IX(1, j)];
                x[IX(N - 1, j)] = b == 1 ? -x[IX(N - 2, j)] : x[IX(N - 2, j)];
            }

            x[IX(0, 0)] = 0.5f * (x[IX(1, 0)] + x[IX(0, 1)]);
            x[IX(0, N - 1)] = 0.5f * (x[IX(1, N - 1)] + x[IX(0, N - 2)]);
            x[IX(N - 1, 0)] = 0.5f * (x[IX(N - 2, 0)] + x[IX(N - 1, 1)]);
            x[IX(N - 1, N - 1)] = 0.5f * (x[IX(N - 2, N - 1)] + x[IX(N - 1, N - 2)]);
        }

        int IX(int x, int y)
        {
            x = Mathf.Clamp(x, 0, N - 1);
            y = Mathf.Clamp(y, 0, N - 1);
            return x + y * N;
        }
    }

    // *****************************
    // SetBounds Jobs
    // *****************************

    public struct ProjectJob : IJobFor
    {
        [ReadOnly] public int N;
        [ReadOnly] public NativeArray<float> velocX;
        [ReadOnly] public NativeArray<float> velocY;
        [WriteOnly] public NativeArray<float> div;
        [WriteOnly] public NativeArray<float> p;

        public void Execute(int index)
        {
            int i = index % N;
            int j = index / N;
            div[IX(i, j)] = -0.5f * (
              velocX[IX(i + 1, j)]
              - velocX[IX(i - 1, j)]
              + velocY[IX(i, j + 1)]
              - velocY[IX(i, j - 1)]
              ) / N;
            p[IX(i, j)] = 0;
        }

        int IX(int x, int y)
        {
            x = Mathf.Clamp(x, 0, N - 1);
            y = Mathf.Clamp(y, 0, N - 1);
            return x + y * N;
        }
    }
}