using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace slide2n
{
    class Program
    {
        /// <summary>
        /// error message
        /// </summary>
        /// <param name="message"></param>

        static void error(string message)
        {
            Console.Error.WriteLine(message);
            System.Environment.Exit(-1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>

        static void Main(string[] args)
        {
            slide2n.slide slidemain = new slide2n.slide();

            if (args.Length != 3)
                error("usage: this.exe [E|D] input_filename output_filename");

            using (FileStream InFile = new FileStream(args[1], FileMode.Open))
            {
                using (FileStream OutFile = new FileStream(args[2], FileMode.Create))
                {
                    if (args[0].StartsWith("e", StringComparison.CurrentCultureIgnoreCase))
                    {
                        // 圧縮
                        try
                        {
                            slidemain.EncodeFile(InFile, OutFile);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                            throw e;
                        }
                    }
                    else
                    {
                        // 展開                            
                        slidemain.DecodeFile(InFile, OutFile);
                    }
                }
            }
        }
    }
}
