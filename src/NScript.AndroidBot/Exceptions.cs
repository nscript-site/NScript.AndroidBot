using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public class BotException : Exception
    {
        public BotException(String msg) : base(msg)
        { }
    }
}
