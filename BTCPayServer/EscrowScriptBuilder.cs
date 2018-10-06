using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BTCPayServer
{
    //<Bob.PubKey>
    //OP_DEPTH OP_3 OP_EQUAL
    //OP_IF

    //	OP_SWAP
    //	<Alice.PubKey> OP_CHECKSIGVERIFY OP_CODESEPARATOR
    //OP_ELSE
    //    0 OP_CLTV OP_DROP
    //OP_ENDIF
    //OP_CHECKSIG
    public class EscrowScriptPubKeyParameters
    {

        public EscrowScriptPubKeyParameters()
        {

        }

        public EscrowScriptPubKeyParameters(PubKey initiator, PubKey receiver, LockTime lockTime)
        {
            this.Initiator = initiator;
            this.Receiver = receiver;
            this.LockTime = lockTime;
        }
        public PubKey Initiator
        {
            get; set;
        }

        public PubKey Receiver
        {
            get; set;
        }
        public LockTime LockTime
        {
            get; set;
        }
        static readonly Comparer<PubKey> LexicographicComparer = Comparer<PubKey>.Create((a, b) => Comparer<string>.Default.Compare(a?.ToHex(), b?.ToHex()));


        // OP_DEPTH 2 OP_EQUAL
        // OP_IF
        //     <Receiver.PubKey> OP_CHECKSIGVERIFY
        // OP_ELSE
        //     0 OP_CLTV OP_DROP
        // OP_ENDIF
        // <Initiator.PubKey> OP_CHECKSIG
        public Script ToRedeemScript()
        {
            if (Initiator == null || Receiver == null || LockTime == default(LockTime))
                throw new InvalidOperationException("Parameters are incomplete");
            EscrowScriptPubKeyParameters parameters = new EscrowScriptPubKeyParameters();
            List<Op> ops = new List<Op>();
            ops.Add(OpcodeType.OP_DEPTH);
            ops.Add(OpcodeType.OP_2);
            ops.Add(OpcodeType.OP_EQUAL);
            ops.Add(OpcodeType.OP_IF);
            {
                ops.Add(Op.GetPushOp(Receiver.ToBytes()));
                ops.Add(OpcodeType.OP_CHECKSIGVERIFY);
            }
            ops.Add(OpcodeType.OP_ELSE);
            {
                ops.Add(Op.GetPushOp(LockTime));
                ops.Add(OpcodeType.OP_CHECKLOCKTIMEVERIFY);
                ops.Add(OpcodeType.OP_DROP);
            }
            ops.Add(OpcodeType.OP_ENDIF);
            ops.Add(Op.GetPushOp(Initiator.ToBytes()));
            ops.Add(OpcodeType.OP_CHECKSIG);
            return new Script(ops.ToArray());
        }


        public override bool Equals(object obj)
        {
            EscrowScriptPubKeyParameters item = obj as EscrowScriptPubKeyParameters;
            if (item == null)
                return false;
            return ToRedeemScript().Equals(item.ToRedeemScript());
        }
        public static bool operator ==(EscrowScriptPubKeyParameters a, EscrowScriptPubKeyParameters b)
        {
            if (System.Object.ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;
            return a.ToRedeemScript() == b.ToRedeemScript();
        }

        public static bool operator !=(EscrowScriptPubKeyParameters a, EscrowScriptPubKeyParameters b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return ToRedeemScript().GetHashCode();
        }

        internal Script ToScript()
        {
            return ToRedeemScript().WitHash.ScriptPubKey.Hash.ScriptPubKey;
        }
    }
}
