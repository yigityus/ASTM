using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Windows.Forms;
using System.Threading;

namespace ASTM
{
    public class Message
    {

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

        char recordType;
        const char pipe = '|';
        const char caret = '^';
        string delimiterDef = @"|\^&";
        string instrument = @"H7600^1";
        string host = "host";

        private int _barcode;
        
        public int Barcode
        {
            get { return _barcode; }
            set { _barcode = value; }
        }

        public Message(int barcode)
        {
            _barcode = barcode;
        }


        public string[] SendOrder(List<string> tests)
        {
            string[] send = new string[6];

            send[0] = enq;
            send[1] = Header();
            send[2] = Patient(_barcode, 30, 'M');
            send[3] = Order(_barcode, tests);
            send[4] = Terminator();
            send[5] = eot;

            return send;
        }



        public string Header()
        {
            string record; 
            recordType = 'H';

            record = "1" + recordType + delimiterDef + new String(pipe, 3) + instrument + new String(pipe, 5) + host + new String(pipe, 2) + "P" + pipe + "1";
            record = record + cr + etx;
            record = stx + record;
            return record;
        }

        public string Terminator()
        {
            string record;
            recordType = 'L';

            record = "4" + "L" + pipe + "1" + pipe + "N";
            record = record + cr + etx; //no checksum.. add [##+cr+lf] if you want...
            record = stx + record;
            return record;
        }

        public string Patient(int barcode, int age, char sex)
        {
            string record;
            recordType = 'P';

            record = "2" + recordType + pipe + "1" + new String(pipe, 2) + barcode.ToString() + new String(pipe, 5) + sex + new String(pipe, 6) + age.ToString() + caret + "Y";
            record = record + cr + etx;
            record = stx + record;
            return record;
        }

        //instrumentSpecimenID
        //comes with the query string was sent
        //<Sample No>^<Rack ID>^<Position No>^^<RackType>^<Container Type>

        virtual public string Order(int barcode, List<string> tests)
        {
            string record;
            string testRecord = "";
            recordType = 'O';

            foreach (string test in tests)
	        {
                testRecord+= test + @"\";
	        }

            if (testRecord!="")
            {
                testRecord = testRecord.Substring(0, testRecord.Length - 1); //trim the last backslash
            }
            

            record = "3" + recordType + pipe + "1" + pipe + barcode + pipe + pipe + testRecord + pipe + "R" /*priority {R, S}*/+
                 new String(pipe, 6) + "N" + //action code {N, new C, cancel}
                 new String(pipe, 14) + "O" + //order record 
                 new String(pipe, 5);
            
            record = record + cr + etx;
            record = stx + record;
            return record;
        }

    }

}
