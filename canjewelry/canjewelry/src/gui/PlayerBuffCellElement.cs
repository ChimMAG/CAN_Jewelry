using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace canjewelry.src.gui
{
    public class PlayerBuffCellElement
    {
        public string BuffName { get; set; }
        public float BuffValue { get; set; }
        public PlayerBuffCellElement(string buffName, float buffValue)
        {
            BuffName = buffName;
            BuffValue = buffValue;
        }
    }
}
