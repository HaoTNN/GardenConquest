using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GardenConquest.Extensions;
using GardenConquest.Records;

namespace GardenConquest.Messaging {
    public class FleetResponse : BaseResponse {

        public List<FactionFleet.GridData> FleetData;
        public GridOwner.OWNER_TYPE OwnerType;

        private const int BaseSize = HeaderSize;

        public FleetResponse()
            : base(BaseResponse.TYPE.FLEET) {

        }

        public override byte[] serialize() {
            VRage.ByteStream bs = new VRage.ByteStream(BaseSize, true);

            byte[] bmessage = base.serialize();
            bs.Write(bmessage, 0, bmessage.Length);

            GridOwner.OWNER owner = GridOwner.ownerFromPlayerID(Destination[0]);
            FactionFleet fleet = GardenConquest.Core.StateTracker.
                getInstance().getFleet(owner.FleetID, owner.OwnerType);

            bs.addUShort((ushort)owner.OwnerType);
            fleet.serialize(bs);

            return bs.Data;
        }

        public override void deserialize(VRage.ByteStream stream) {
            base.deserialize(stream);
            OwnerType = (GridOwner.OWNER_TYPE)stream.getUShort();
            FleetData = new List<FactionFleet.GridData>(FactionFleet.deserialize(stream));
        }


    }
}
