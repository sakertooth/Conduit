using System.Collections.Generic;
using System.Net;

namespace Conduit
{
    class PortRange
    {
        public static bool TryParse(string portRangeString, out ICollection<int> ports)
        {
            ports = new List<int>(ushort.MaxValue);
            foreach (var range in portRangeString.Split(','))
            {
                if (range.Contains('-'))
                {
                    var rangeIndex = range.IndexOf('-');
                    if (!int.TryParse(range.Substring(0, rangeIndex), out int rangeMin) 
                      || !int.TryParse(range.Substring(rangeIndex + 1), out int rangeMax))
                    {
                        return false;
                    }

                    if (rangeMin < IPEndPoint.MinPort || rangeMin > IPEndPoint.MaxPort ||
                        rangeMax < rangeMin || rangeMax < IPEndPoint.MinPort || rangeMax > IPEndPoint.MaxPort)
                    {
                        return false;
                    }

                    for (int i = rangeMin; i <= rangeMax; i++)
                    {
                        ports.Add(i);
                    }
                }
                else
                {
                    if (!int.TryParse(range, out int port))
                    {
                        return false;
                    }

                    ports.Add(port);
                }
            }

            return true;
        }
    }
}
