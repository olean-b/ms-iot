namespace KNXLibPortableLib.DPT
{
    public abstract class DataPoint
    {
        public abstract string[] Ids { get; }

        public abstract object FromDataPoint(string data, bool isDebug = false);

        public abstract object FromDataPoint(byte[] data, bool isDebug = false);

        public abstract byte[] ToDataPoint(string value, bool isDebug = false);

        public abstract byte[] ToDataPoint(object value, bool isDebug = false);
    }
}
