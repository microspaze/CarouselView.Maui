using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarouselView.Abstractions
{
    [ContentProperty("DataTemplate")]
    public class CarouselViewPositionTemplate
    {
        public int Position { get; set; }

        public DataTemplate? DataTemplate { get; set; }
    }
}
