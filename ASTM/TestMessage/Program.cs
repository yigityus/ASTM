using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ASTM;
using System.IO.Ports;
using System.Threading;
using System.Data.OracleClient;

namespace TestMessage
{
    class Program
    {
        const int wait = 3000;
        const int cihazId = 2;

        static string soh = char.ConvertFromUtf32(1);
        static string stx = char.ConvertFromUtf32(2);
        static string etx = char.ConvertFromUtf32(3);
        static string eot = char.ConvertFromUtf32(4);
        static string enq = char.ConvertFromUtf32(5);
        static string ack = char.ConvertFromUtf32(6);
        static string nack = char.ConvertFromUtf32(21);
        static string etb = char.ConvertFromUtf32(23);
        static string lf = char.ConvertFromUtf32(10);
        static string cr = char.ConvertFromUtf32(13);


        static SerialPort sp;
        static EventWaitHandle wh;
        
        static bool orderFlag = false;
        static string connStr = "data source=localhost;user id=chz;password=chz";
        static OracleConnection conn = new OracleConnection(connStr);

        static void Main(string[] args)
        {
            Console.WindowWidth = 120;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Title = "Host";
            
            //Console.WriteLine(Console.WindowWidth.ToString());

            sp = new SerialPort("COM1");
            sp.DataReceived+=new SerialDataReceivedEventHandler(sp_DataReceived);
            sp.Open();

            Console.ReadKey();
        }

        static void sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string str = sp.ReadExisting();
            Eval(str);
        }


        private static void Eval(string str)
        {
            string received;
            
            Thread thread;

            received = null;

            received = str;
            Console.WriteLine("[received]" + Display(received));

            if (received == ack)
            {
                wh.Set();

            }
            else if (received == enq)
            {
                ACK();
            }

            else if (received == eot)
            {
                if (orderFlag)
                {
                    thread = new Thread(new ThreadStart(SerialWrite));
                    thread.Start();
                    orderFlag = false;
                    Console.WriteLine("[Host ~]$ Analyzer Queries {0}", _barcode);
                    Console.WriteLine("[Host ~]$ Retrieving {0} tests from LIS", _barcode);
                }
            }
            else
            {
                string recordType = received[2].ToString();

                switch (recordType)
                {
                    case "H":
                        ACK();
                        break;
                    case "Q":
                        string[] fields = received.Split('|');
                        string[] comps = fields[2].Split('^');
                        string barcode = comps[1];

                        _barcode = Convert.ToInt32(barcode);
                        
                        orderFlag = true;
                        ACK();
                        break;
                    case "R":
                        string test = "";
                        string result = "";
                        string resultUnit = "";
                        test = received.Split('|').GetValue(2).ToString();
                        result = received.Split('|').GetValue(3).ToString();
                        resultUnit = received.Split('|').GetValue(4).ToString();
                        Update(_barcode, test, result, resultUnit);
                        ACK();
                        break;
                    case "O":
                        _barcode = Convert.ToInt32(received.Split('|').GetValue(2).ToString());
                        ACK();
                        break;
                    case "L":
                        ACK();
                        break;
                    default:
                        ACK();
                        break;
                }

            }

        }

        private static void Update(int _barcode, string test, string result, string resultUnit)
        {
            Console.WriteLine("[Host ~]$ Update " + _barcode.ToString() + " " + test + " = " + result + " " + resultUnit);

            string sql = "update CHZ_ORDER set PENDING= 'C', RESULT= '" + result +
                "' , RESULT_UNIT= '" + resultUnit +
                "' , RESULT_DATE= CURRENT_TIMESTAMP, CIHAZ_ID= " + cihazId.ToString() +
                " where TUBE_BARCODE= " + _barcode.ToString() +
                " and LIS_TEST=(select LIS_TEST from CHZ_TEST where CIHAZ_TEST= '" + test +
                "' and CIHAZ_ID= " + cihazId.ToString() + ")";

           

            OracleCommand cmd = new OracleCommand(sql, conn);

            try
            {
                conn.Open();
                cmd.ExecuteNonQuery();

            }


            catch (OracleException OEx)
            {
                Console.WriteLine(OEx.Message);
            }
            finally
            {
                conn.Close();
                cmd.Dispose();
            }



        }

        private static void ACK()
        {
            Thread.Sleep(wait);
            sp.Write(ack);
        }

        private static int _barcode;

        static void SerialWrite()
        {
            wh = new AutoResetEvent(false);

            Message m = new Message(_barcode);

            string sql = "select C.CIHAZ_TEST from CHZ_ORDER O, CHZ_TEST C where O.TUBE_BARCODE=" +
                _barcode.ToString() + "and C.CIHAZ_ID=" + cihazId + "and C.LIS_TEST=O.LIS_TEST and O.PENDING='P'";
            
            OracleCommand cmd = new OracleCommand(sql, conn);
            List<string> tests = new List<string>();
            OracleDataReader read;
            try
            {
                conn.Open();
                read = cmd.ExecuteReader();


                while (read.Read())
                {
                    tests.Add(read[0].ToString());
                }

            }


            catch (OracleException OEx)
            {
                Console.WriteLine(OEx.Message);
            }
            finally
            {
                conn.Close();
                cmd.Dispose();
            }
 

            string[] send = m.SendOrder(tests);

            for (int i = 0; i < send.Length; i++)
            {
                sp.Write(send[i]);
                wh.WaitOne();
            }

            Console.WriteLine("[Host ~]$ ordered: {0}", "tests");
        }




        static readonly string[] LowNames =
        {
                "<NUL>", "<SOH>", "<STX>", "<ETX>", "<EOT>", "<ENQ>", "<ACK>", "<BEL>", 
                "<BS>", "<HT>", "<LF>", "<VT>", "<FF>", "<CR>", "<SO>", "<SI>",
                "<DLE>", "<DC1>", "<DC2>", "<DC3>", "<DC4>", "<NAK>", "<SYN>", "<ETB>",
                "<CAN>", "<EM>", "<SUB>", "<ESC>", "<FS>", "<GS>", "<RS>", "<US>"
        };
        
        
        



        static string Display(string received)
        {
            string display = null;

            foreach (char c in received)
            {
                if (c < 32)
                {
                    display += LowNames[c];
                }
                else
                {
                    display += c;
                }
            }

            return display;
        }



        
    }
}
