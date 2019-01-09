using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace slide2n
{
    class slide
    {
        // 単純スライド辞書圧縮の整理書き直し
        // 展開部の基礎テスト
        // 圧縮コードの改良テストその１
        // ※4Bit一致長コードを非線形にしてゲーム用データに最適化するテスト
        // （最長一致長が大幅に増加しているので注意すること）

        private const int N = 4096; // 環状バッファの大きさ
        private const int F = 64; // 最長一致長（オリジナル：18）
        private const int NIL = N; // 木の末端

        long outcount = 0;				        // 出力バイト数カウンタ
        byte[] text = new byte[N + F - 1];		// テキスト用バッファ
        int[] dad = new int[N + 1];
        int[] lson = new int[N + 1];
        int[] rson = new int[N + 257];	        // 木


        // code(4bit)                             0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15
        //static const int MatchLenSizeNormal[] =  {  3, 4, 5, 6, 7, 8, 9,10,11,12,13,14,15,16,17,18 }; // オリジナル
        //#define MATCHLEN_TABLE_SIZE (16)
        static readonly int[] MatchLenSizeTable = new int[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 18, 21, 27, 41, F }; // 微調整版

        // 一致長符号の生成

        int ConvMatchLenCode(int len, out int code)
        {
            if (len < MatchLenSizeTable[0])
            {
                code = 0;
                return 1;
            }

            for (int i = 0; i < MatchLenSizeTable.Length; i++)
            {
                if (len < MatchLenSizeTable[i])
                {
                    code = i - 1;
                    return MatchLenSizeTable[i - 1];
                }
            }

            code = MatchLenSizeTable.Length - 1;
            return MatchLenSizeTable[MatchLenSizeTable.Length - 1];
        }


        /// <summary>
        /// 木の初期化
        /// </summary>

        void init_tree()
        {
            for (int i = N + 1; i <= N + 256; i++)
                rson[i] = NIL;

            for (int i = 0; i < N; i++)
                dad[i] = NIL;
        }


        // --------------------------------------------------------------------------------------

        int matchpos, matchlen;  // 最長一致位置, 一致長

        /// <summary>
        /// 節 r を木に挿入
        /// </summary>
        /// <param name="r"></param>

        void insert_node(int r)
        {
            int i, p, cmp;
            int key;

            cmp = 1;
            key = r;
            p = N + 1 + text[key];

            rson[r] = lson[r] = NIL;
            matchlen = 0;

            while (true)
            {
                if (cmp >= 0)
                {
                    if (rson[p] != NIL)
                        p = rson[p];
                    else
                    {
                        rson[p] = r;
                        dad[r] = p;
                        return;
                    }
                }
                else
                {
                    if (lson[p] != NIL)
                        p = lson[p];
                    else
                    {
                        lson[p] = r;
                        dad[r] = p;
                        return;
                    }
                }

                for (i = 1; i < F; i++)
                {
                    if ((cmp = text[key + i] - text[p + i]) != 0)
                        break;
                }

                if (i > matchlen)
                {
                    matchpos = p;
                    if ((matchlen = i) >= F)
                        break;
                }
            }

            dad[r] = dad[p];
            lson[r] = lson[p];
            rson[r] = rson[p];

            dad[lson[p]] = r;
            dad[rson[p]] = r;

            if (rson[dad[p]] == p)
                rson[dad[p]] = r;
            else
                lson[dad[p]] = r;

            dad[p] = NIL;  // p を外す
        }


        /// <summary>
        ///  節 p を木から消す
        /// </summary>
        /// <param name="p"></param>

        void delete_node(int p)
        {
            int q;

            if (dad[p] == NIL)
                return;  // 見つからない

            if (rson[p] == NIL)
                q = lson[p];

            else if (lson[p] == NIL)
                q = rson[p];

            else
            {
                q = lson[p];

                if (rson[q] != NIL)
                {

                    do
                    {
                        q = rson[q];
                    } while (rson[q] != NIL);

                    rson[dad[q]] = lson[q];
                    dad[lson[q]] = dad[q];

                    lson[q] = lson[p];
                    dad[lson[p]] = q;
                }
                rson[q] = rson[p];
                dad[rson[p]] = q;

            }

            dad[q] = dad[p];

            if (rson[dad[p]] == p)
                rson[dad[p]] = q;
            else
                lson[dad[p]] = q;

            dad[p] = NIL;
        }


        /// <summary>
        /// 圧縮
        /// </summary>
        /// <param name="infile"></param>
        /// <param name="outfile"></param>
        public void EncodeFile(FileStream infile, FileStream outfile)
        {
            int len, r, s, lastmatchlen, codeptr;
            byte c, mask;
            byte[] code = new byte[100];
            long incount = 0, printcount = 0, cr;
            int matchlencode;

            init_tree();

            code[0] = 0;
            codeptr = mask = 1;
            s = 0;
            r = N - F;

            for (int i = s; i < r; i++)
                text[i] = 0;  // バッファを初期化

            using (BinaryReader br = new BinaryReader(infile))
            {
                using (BinaryWriter bw = new BinaryWriter(outfile))
                {

                    for (len = 0; len < F; len++)
                    {
                        //if ((c = fgetc(infile)) == EOF) break;
                        try
                        {
                            c = br.ReadByte();
                        }
                        catch (EndOfStreamException)
                        {
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.Error.Write(e.ToString());
                            throw e;
                        }

                        text[r + len] = c;
                    }

                    incount = len;

                    if (incount == 0)
                        return;

                    for (int i = 1; i <= F; i++)
                        insert_node(r - i);

                    insert_node(r);

                    do
                    {
                        if (matchlen > len)
                            matchlen = len;

                        if (matchlen < 3)
                        {
                            matchlen = 1;
                            code[0] |= (byte)(mask & 0xff);
                            code[codeptr++] = text[r];
                        }
                        else
                        {
                            matchlen = ConvMatchLenCode(matchlen, out matchlencode);
                            code[codeptr++] = (byte)matchpos;
                            code[codeptr++] = (byte)(((matchpos >> 4) & 0xf0) | matchlencode); //(matchlen-3));
                        }

                        if ((mask <<= 1) == 0)
                        {
                            for (int i = 0; i < codeptr; i++)
                            {
                                bw.Write(code[i]);
                            }

                            outcount += codeptr;
                            code[0] = 0;
                            codeptr = mask = 1;
                        }

                        lastmatchlen = matchlen;

                        int count;
                        for (count = 0; count < lastmatchlen; count++)
                        {
                            try
                            {
                                c = br.ReadByte();
                            }
                            catch (EndOfStreamException)
                            {
                                break;
                            }
                            catch (Exception e)
                            {
                                Console.Error.Write(e.ToString());
                                throw e;
                            }

                            delete_node(s);
                            text[s] = c;

                            if (s < F - 1)
                                text[s + N] = c;

                            s = ++s & (N - 1);
                            r = ++r & (N - 1);

                            insert_node(r);
                        }

                        if ((incount += count) > printcount)
                        {
                            Console.Write("{0}\r", incount);
                            printcount += 1024;
                        }

                        while (count++ < lastmatchlen)
                        {
                            delete_node(s);
                            s = (s + 1) & (N - 1);
                            r = (r + 1) & (N - 1);

                            if (--len > 0)
                                insert_node(r);
                        }

                    } while (len > 0);

                    if (codeptr > 1)
                    {
                        for (int i = 0; i < codeptr; i++)
                        {
                            bw.Write(code[i]);
                        }

                        outcount += codeptr;
                    }

                    Console.Write("In : {0} bytes\n", incount);  // 結果報告
                    Console.Write("Out: {0} bytes\n", outcount);

                    if (incount != 0)
                    {
                        // 圧縮比を求めて報告
                        cr = (1000 * outcount + incount / 2) / incount;
                        Console.Write("Out/In: {0}.{1}\n", cr / 1000, cr % 1000);
                    }
                }
            }
        }


        /// <summary>
        /// 展開
        /// </summary>
        /// <param name="infile"></param>
        /// <param name="outfile"></param>
        /// <param name="?"></param>

        public void DecodeFile(FileStream infile, FileStream outfile)
        {
            byte c;
            int flags;

            for (int i = 0; i < N - F; i++)
                text[i] = 0;

            int r = N - F;

            using (BinaryReader br = new BinaryReader(infile))
            {
                using (BinaryWriter bw = new BinaryWriter(outfile))
                {
                    while (true)
                    {
                        try
                        {
                            c = br.ReadByte();

                        }
                        catch (EndOfStreamException)
                        {
                            goto decode_end;
                        }
                        catch (Exception e)
                        {
                            throw e;
                        }

                        flags = ((int)c) | 0xff00;

                        while ((flags & 0xff00) != 0)
                        {
                            if ((flags & 0x01) != 0)
                            {
                                //if ((c = fgetc(infile)) == EOF)
                                //    goto decode_end;
                                try
                                {
                                    c = br.ReadByte();
                                }
                                catch (EndOfStreamException)
                                {
                                    goto decode_end;
                                }
                                catch (Exception e)
                                {
                                    throw e;
                                }

                                bw.Write(c);

                                text[r++] = c;
                                r &= (N - 1);
                            }
                            else
                            {
                                int i, j;

                                try
                                {
                                    i = br.ReadByte();
                                    j = br.ReadByte();
                                }
                                catch (EndOfStreamException)
                                {
                                    goto decode_end;
                                }
                                catch (Exception e)
                                {
                                    throw e;
                                }

                                i |= ((j & 0xf0) << 4);

                                //j = (j & 0x0f) + 3;
                                j = MatchLenSizeTable[j & 0x0f];

                                for (int k = 0; k < j; k++)
                                {
                                    c = text[(i + k) & (N - 1)];

                                    bw.Write(c);

                                    text[r++] = c;
                                    r &= (N - 1);
                                }
                            }

                            flags >>= 1;
                        }
                    }

                decode_end: ;
                }
            }
        }


        /// <summary>
        /// メモリ上で展開（sizeは）
        /// </summary>
        /// <param name="data">入力データ</param>
        /// <param name="size">展開後のサイズ</param>
        /// <returns></returns>

        public byte[] Decode(byte[] data)
        {
            int size = data.Length;

            if (size <= 1)
                return null;

            for (int i = 0; i < N - F; i++)
                text[i] = 0;

            int r = N - F;
            int flags;
            int index = 0;
            byte[] result;

            using (MemoryStream ms = new MemoryStream(data))
            {
                while (true)
                {
                    int c = (int)data[index++];
                    flags = c | 0xff00;

                    while ((flags & 0xff00) != 0)
                    {
                        if ((flags & 0x01) != 0)
                        {
                            ms.WriteByte(data[index++]);
                            if (--size == 0) goto decode_end;

                            text[r++] = (byte)(c & 0xff);
                            r &= (N - 1);
                        }
                        else
                        {
                            int i = data[index++];
                            int j = data[index++];

                            i |= ((j & 0xf0) << 4);
                            j = MatchLenSizeTable[j & 0x0f];

                            for (int k = 0; k < j; k++)
                            {
                                ms.WriteByte(text[(i + k) & (N - 1)]);
                                if (--size == 0) goto decode_end;

                                text[r++] = (byte)(c & 0xff);
                                r &= (N - 1);
                            }
                        }

                        flags >>= 1;
                    }
                }

            decode_end: ;
                result = ms.ToArray();
            }

            return result;
        }

    }
}
