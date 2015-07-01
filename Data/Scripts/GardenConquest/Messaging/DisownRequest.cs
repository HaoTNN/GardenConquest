using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GardenConquest.Messaging {

    /// <summary>
    /// Requests server to disown a client's grid
    /// </summary>
    public class DisownRequest : BaseRequest {
        public long EntityID;

        public DisownRequest()
            : base(BaseRequest.TYPE.DISOWN) {
            
        }
    }
}
