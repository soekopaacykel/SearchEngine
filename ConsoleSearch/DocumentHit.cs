using System;
using System.Collections.Generic;
using Shared.Model;

namespace ConsoleSearch
{
    public class DocumentHit
    {
        public DocumentHit(BEDocument doc, int noOfHits, List<string> missing)
        {
            Document = doc;
            NoOfHits = noOfHits;
            Missing = missing;
        }

        public BEDocument Document { get;  }

        public int NoOfHits { get;  }

        public List<string> Missing { get;  }
    }
}
