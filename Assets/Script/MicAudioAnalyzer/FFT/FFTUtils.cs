using UnityEngine;

public struct Complex
{
    public float Real;
    public float Imag;

    public Complex(float real, float imag)
    {
        Real = real;
        Imag = imag;
    }

    // 复数运算重载（FFT需要的加减乘）
    public static Complex operator +(Complex a, Complex b) => new(a.Real + b.Real, a.Imag + b.Imag);
    public static Complex operator -(Complex a, Complex b) => new(a.Real - b.Real, a.Imag - b.Imag);
    public static Complex operator *(Complex a, Complex b) => new(
        a.Real * b.Real - a.Imag * b.Imag, 
        a.Real * b.Imag + a.Imag * b.Real
    );
}

// FFT算法静态类：提供快速傅里叶变换方法
public static class FFT
{
    /// <summary>
    /// 执行FFT变换（将时域复数数组转为频域）
    /// </summary>
    /// <param name="data">输入/输出的复数数组（长度必须是2的幂）</param>
    /// <param name="invert">是否执行逆变换（时域→频域用false，频域→时域用true）</param>
    public static void Compute(Complex[] data, bool invert)
    {
        int n = data.Length;

        // 1. 位反转（FFT的前置操作，重新排列输入数据）
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; j >= bit; bit >>= 1)
                j -= bit;
            j += bit;
            if (i < j)
                Swap(ref data[i], ref data[j]);
        }

        // 2. 蝴蝶操作（FFT的核心，分治计算）
        for (int length = 2; length <= n; length <<= 1)
        {
            float ang = 2 * Mathf.PI / length * (invert ? -1 : 1);
            Complex wlen = new(Mathf.Cos(ang), Mathf.Sin(ang));
            for (int i = 0; i < n; i += length)
            {
                Complex w = new(1, 0);
                for (int j = 0; j < length / 2; j++)
                {
                    Complex u = data[i + j];
                    Complex v = data[i + j + length / 2] * w;
                    data[i + j] = u + v;
                    data[i + j + length / 2] = u - v;
                    w *= wlen;
                }
            }
        }

        // 3. 逆变换时归一化（如果是频域转时域，需要除以长度）
        if (invert)
        {
            for (int i = 0; i < n; i++)
                data[i] = new(data[i].Real / n, data[i].Imag / n);
        }
    }

    // 交换两个复数（辅助方法）
    private static void Swap(ref Complex a, ref Complex b)
    {
        Complex temp = a;
        a = b;
        b = temp;
    }
}
