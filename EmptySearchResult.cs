using Redbox.HAL.Component.Model;
using System;
using System.Collections.Generic;

namespace Redbox.HAL.Controller.Framework
{
    internal sealed class EmptySearchResult : IEmptySearchResult, IDisposable
    {
        private bool m_disposed;

        public void Dispose()
        {
            if (this.m_disposed)
                return;
            this.m_disposed = true;
            this.EmptyLocations.Clear();
            GC.SuppressFinalize((object)this);
        }

        public IList<ILocation> EmptyLocations { get; private set; }

        public int FoundEmpty => this.EmptyLocations.Count;

        internal EmptySearchResult() => this.EmptyLocations = (IList<ILocation>)new List<ILocation>();
    }
}
