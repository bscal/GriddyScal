using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Common.FluidSimulation
{
    public struct Fluid
    {
        int N;
        float visc;
        float diff;
        float dt;
        float[] Vx;
        float[] Vx0;
        float[] Vy;
        float[] Vy0;

        float[] s;
        float[] d;
    }

    [RequireComponent(typeof(TileMap2DArray))]
    public class FluidSimTileMap : MonoBehaviour
    {

        public TileMap2DArray TileMap;

        Fluid Fluid;

        int N;
        int iterations = 1;
        float visc = 0.0000001f;
        float diff = 0;
        float dt = 0.2f;
        float[] Vx;
        float[] Vx0;
        float[] Vy;
        float[] Vy0;

        float[] s;
        float[] d;

        int w;
        int w0;

        private void Start()
        {
            N = (TileMap.MapSize.x) * (TileMap.MapSize.y);
            Vx = new float[N];
            Vx0 = new float[N];
            Vy = new float[N];
            Vy0 = new float[N];
            s = new float[N];
            d = new float[N];
            w = TileMap.MapSize.x;
            w0 = TileMap.MapSize.x - 1;
        }

        int IX(int j, int i) => Mathf.Clamp(j, 0, w0) + Mathf.Clamp(i, 0, w0) * w;

        void Update()
        {
            Diffuse(1, Vx0, Vx, visc, dt);
            Diffuse(2, Vy0, Vy, visc, dt);

            Project(Vx0, Vy0, Vx, Vy);

            Advect(1, Vx, Vx0, Vx0, Vy0, dt);
            Advect(2, Vy, Vy0, Vx0, Vy0, dt);

            Project(Vx, Vy, Vx0, Vy0);

            Diffuse(0, s, d, diff, dt);
            Advect(0, d, s, Vx, Vy, dt);

            SetColors();
        }

        private void SetColors()
        {
            int x = 0, y = 0;
            var colors = TileMap.GetTileColor();
            for (int j = 1; j < N - 1; j++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    Color c = Color.Lerp(Color.white, Color.blue, d[IX(j, i)]);
                    TileMap.SetColor(IX(j, i), c, colors);
                    x++;
                }
                y++;
            }
            TileMap.SetTileColor(colors);
        }

        void Diffuse(int b, float[] x, float[] x0, float diff, float dt)
        {
            float a = dt * diff * (N - 2) * (N - 2);
            Lin_solve(b, x, x0, a, 1 + 4 * a);
        }

        void Lin_solve(int b, float[] x, float[] x0, float a, float c)
        {
            float cRecip = 1.0f / c;
            for (int k = 0; k < iterations; k++)
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

                Set_bnd(b, x);
            }
        }

        void Project(float[] velocX, float[] velocY, float[] p, float[] div)
        {
            for (int j = 1; j < N - 1; j++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    div[IX(i, j)] = -0.5f * (
                      velocX[IX(i + 1, j)]
                      - velocX[IX(i - 1, j)]
                      + velocY[IX(i, j + 1)]
                      - velocY[IX(i, j - 1)]
                      ) / N;
                    p[IX(i, j)] = 0;
                }
            }

            Set_bnd(0, div);
            Set_bnd(0, p);
            Lin_solve(0, p, div, 1, 4);

            for (int j = 1; j < N - 1; j++)
            {
                for (int i = 1; i < N - 1; i++)
                {
                    velocX[IX(i, j)] -= 0.5f * (p[IX(i + 1, j)]
                      - p[IX(i - 1, j)]) * N;
                    velocY[IX(i, j)] -= 0.5f * (p[IX(i, j + 1)]
                      - p[IX(i, j - 1)]) * N;
                }
            }
            Set_bnd(1, velocX);
            Set_bnd(2, velocY);
        }

        void Advect(int b, float[] d, float[] d0, float[] velocX, float[] velocY, float dt)
        {
            float i0, i1, j0, j1;

            float dtx = dt * (N - 2);
            float dty = dt * (N - 2);

            float s0, s1, t0, t1;
            float tmp1, tmp2, x, y;

            float Nfloat = N;
            float ifloat, jfloat;
            int i, j;

            for (j = 1, jfloat = 1; j < N - 1; j++, jfloat++)
            {
                for (i = 1, ifloat = 1; i < N - 1; i++, ifloat++)
                {
                    tmp1 = dtx * velocX[IX(i, j)];
                    tmp2 = dty * velocY[IX(i, j)];
                    x = ifloat - tmp1;
                    y = jfloat - tmp2;

                    if (x < 0.5f) x = 0.5f;
                    if (x > Nfloat + 0.5f) x = Nfloat + 0.5f;
                    i0 = Mathf.Floor(x);
                    i1 = i0 + 1.0f;
                    if (y < 0.5f) y = 0.5f;
                    if (y > Nfloat + 0.5f) y = Nfloat + 0.5f;
                    j0 = Mathf.Floor(y);
                    j1 = j0 + 1.0f;

                    s1 = x - i0;
                    s0 = 1.0f - s1;
                    t1 = y - j0;
                    t0 = 1.0f - t1;

                    int i0i = (int)i0;
                    int i1i = (int)i1;
                    int j0i = (int)j0;
                    int j1i = (int)j1;

                    // DOUBLE CHECK THIS!!!
                    d[IX(i, j)] =
                      s0 * (t0 * d0[IX(i0i, j0i)] + t1 * d0[IX(i0i, j1i)]) +
                      s1 * (t0 * d0[IX(i1i, j0i)] + t1 * d0[IX(i1i, j1i)]);
                }
            }

            Set_bnd(b, d);
        }

        void Set_bnd(int b, float[] x)
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

        public struct DiffuseJob : IJob
        {
            int N;
            int iters;
            int b;
            NativeArray<float> x;
            NativeArray<float> x0;
            float diff;
            float dt;

            public void Execute()
            {
            }
        }

    }


}