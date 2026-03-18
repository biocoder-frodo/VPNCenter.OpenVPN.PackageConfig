namespace VPNCenter.OpenVPN.PackageConfig
{
    class Routes : SortedDictionary<int, Route>
    {
        public Route? Server { get; private set; }

        public bool SetServerRange { get; }
        
        
        public Routes() { }
        private Routes(bool setServerRange) => SetServerRange = true;


        public void AddServerSubnet(Route value)
        {
            value = new Route(value, new Routes(true));

            var remove = new List<int>();
            foreach (var idx in this.Keys)
            {
                if (idx != 0 && this[idx].ToString(true) == value.ToString(true))
                {
                    remove.Add(idx);
                }
            }
            foreach (var r in remove) this.Remove(r);

            if (Server is null)
            {
                this.Add(0, value);
            }
            else
            {
                this[0] = value;
            }
            Server = value;
        }
        public void Add(Route route)
        {
            if (this.Any(r => r.Value.ToString(true) == route.ToString(true)) == false)
            {
                this.Add(this.Count + 1, route);
            }
        }
    }
}
