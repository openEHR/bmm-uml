using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace R4C_BMM_EA_Extension
{
    namespace BMM
    {
        abstract class Base
        {
        }

        class CommentBlock : Base
        {
        }

        class KeyValueLine : Base
        {
            string key;
            string svalue;
            bool? bvalue;

            public KeyValueLine(string key, string value)
            {
                svalue = value;
            }

            public KeyValueLine(string key, bool value)
            {
                bvalue = value;
            }

            public override string ToString()
            {
                if (svalue != null)
                {
                    return string.Format("{0} = <\"{1}\">", key, svalue);
                }
                else if (bvalue != null)
                {
                    return string.Format("{0} = <{1}>", key, bvalue);
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
