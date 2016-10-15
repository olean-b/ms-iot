namespace KNXLibPortableLib.DPT
{
    using System.Globalization;

    public sealed class DataPoint8BitNoSignNonScaledValue1UCount : DataPoint
    {
        public override string[] Ids
        {
            get { return new[] { "5.010" }; }
        }

        public override object FromDataPoint(string data, bool isDebug = false)
        {
            var dataConverted = new byte[data.Length];
            for (var i = 0; i < data.Length; i++)
                dataConverted[i] = (byte)data[i];

            return this.FromDataPoint(dataConverted, isDebug);
        }

        public override object FromDataPoint(byte[] data, bool isDebug = false)
        {
            if (data == null || data.Length != 1)
                return 0;

            return (int)data[0];
        }

        public override byte[] ToDataPoint(string value, bool isDebug = false)
        {
            return this.ToDataPoint(float.Parse(value, CultureInfo.InvariantCulture), isDebug);
        }

        public override byte[] ToDataPoint(object val, bool isDebug = false)
        {
            var dataPoint = new byte[1];
            dataPoint[0] = 0x00;

            int input = 0;
            if (val is int)
                input = ((int)val);
            else if (val is float)
                input = (int)((float)val);
            else if (val is long)
                input = (int)((long)val);
            else if (val is double)
                input = (int)((double)val);
            else if (val is decimal)
                input = (int)((decimal)val);
            else
            {
                if (isDebug)
                {
                    System.Diagnostics.Debug.WriteLine("5.010", "input value received is not a valid type");
                }
                return dataPoint;
            }

            if (input < 0 || input > 255)
            {
                if (isDebug)
                {
                    System.Diagnostics.Debug.WriteLine("5.010", "input value received is not in a valid range");
                }
                return dataPoint;
            }

            dataPoint[0] = (byte)input;

            return dataPoint;
        }
    }
}