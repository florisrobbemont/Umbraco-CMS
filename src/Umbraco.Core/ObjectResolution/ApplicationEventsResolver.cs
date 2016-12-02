using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Umbraco.Core.Logging;
using umbraco.interfaces;

namespace Umbraco.Core.ObjectResolution
{
    /// <summary>
	/// A resolver to return all IApplicationEvents objects
	/// </summary>
	/// <remarks>
	/// This is disposable because after the app has started it should be disposed to release any memory being occupied by instances.
	/// </remarks>
    public sealed class ApplicationEventsResolver : ManyObjectsResolverBase<ApplicationEventsResolver, IApplicationEventHandler>, IDisposable
	{
	    private readonly LegacyStartupHandlerResolver _legacyResolver;

	    /// <summary>
	    /// Constructor
	    /// </summary>
	    /// <param name="logger"></param>
	    /// <param name="applicationEventHandlers"></param>
	    /// <param name="serviceProvider"></param>
	    internal ApplicationEventsResolver(IServiceProvider serviceProvider, ILogger logger, IEnumerable<Type> applicationEventHandlers)
            : base(serviceProvider, logger, applicationEventHandlers)
		{
            //create the legacy resolver and only include the legacy types
	        _legacyResolver = new LegacyStartupHandlerResolver(
                serviceProvider, logger,
	            applicationEventHandlers.Where(x => TypeHelper.IsTypeAssignableFrom<IApplicationEventHandler>(x) == false));
		}

        /// <summary>
        /// Override in order to only return types of IApplicationEventHandler and above,
        /// do not include the legacy types of IApplicationStartupHandler
        /// </summary>
        protected override IEnumerable<Type> InstanceTypes
        {
            get { return base.InstanceTypes.Where(TypeHelper.IsTypeAssignableFrom<IApplicationEventHandler>); }
        }

	    private List<IApplicationEventHandler> _orderedAndFiltered;

        /// <summary>
        /// Gets the <see cref="IApplicationEventHandler"/> implementations.
        /// </summary>
        public IEnumerable<IApplicationEventHandler> ApplicationEventHandlers
		{
	        get
	        {
	            if (_orderedAndFiltered == null)
	            {
	                _resolved = true;
                    _orderedAndFiltered = GetSortedValues().ToList();

                    //call the callback if one is applied
                    if (FilterCollection != null)
                    {
                        FilterCollection(_orderedAndFiltered);
                    }
                }
	            return _orderedAndFiltered;
	        }
		}
        
        /// <summary>
        /// EXPERT: Allow a delegate to be set to filters the event handler list
        /// </summary>
        /// <remarks>
        /// This allows custom logic to execute in order to filter or re-order the event handlers prior to executing.
        /// Primarily this is used for custom boot sequences where the custom boot loader may need to remove certain startup
        /// handlers from executing. 
        /// This can be set on startup in the pre-boot process in either a custom boot manager or global.asax (UmbracoApplication)
        /// </remarks>
        public Action<IList<IApplicationEventHandler>> FilterCollection
	    {
	        get { return _filterCollection; }
	        set
	        {
                if (_resolved)
                    throw new InvalidOperationException("Cannot set the FilterCollection delegate once the ApplicationEventHandlers are resolved");
                if (_filterCollection != null)
                    throw new InvalidOperationException("Cannot set the FilterCollection delegate once it's already been specified");

                _filterCollection = value;
	        }
	    }

        /// <summary>
        /// This will get the object weight for the plugin
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        /// <remarks>
        /// For event handlers, all Core event handlers that are not attributed will result in a -100 value
        /// </remarks>
	    protected override int GetObjectWeight(object o)
	    {
            var type = o.GetType();
            var attr = type.GetCustomAttribute<WeightAttribute>(true);

	        if (attr == null &&
	            (o.GetType().Assembly.FullName.StartsWith("Umbraco.", StringComparison.OrdinalIgnoreCase)
	             || o.GetType().Assembly.FullName.StartsWith("Concorde.")))
	        {
	            return -100;
	        }

	        return attr == null ? DefaultPluginWeight : attr.Weight;
        }
        

        /// <summary>
        /// Create instances of all of the legacy startup handlers
        /// </summary>
	    public void InstantiateLegacyStartupHandlers()
	    {
            //this will instantiate them all
	        var handlers = _legacyResolver.LegacyStartupHandlers;
	    }

		protected override bool SupportsClear
		{
            get { return false; }
		}

		protected override bool SupportsInsert
		{
			get { return false; }
		}

	    private class LegacyStartupHandlerResolver : ManyObjectsResolverBase<ApplicationEventsResolver, IApplicationStartupHandler>, IDisposable
	    {
	        internal LegacyStartupHandlerResolver(IServiceProvider serviceProvider, ILogger logger, IEnumerable<Type> legacyStartupHandlers)
                : base(serviceProvider, logger, legacyStartupHandlers)
	        {

	        }

            public IEnumerable<IApplicationStartupHandler> LegacyStartupHandlers
            {
                get { return Values; }
            }

	        public void Dispose()
	        {
                ResetCollections();
	        }
	    }

	    private bool _disposed;
		private readonly ReaderWriterLockSlim _disposalLocker = new ReaderWriterLockSlim();
	    private Action<IList<IApplicationEventHandler>> _filterCollection;
	    private bool _resolved = false;

	    /// <summary>
		/// Gets a value indicating whether this instance is disposed.
		/// </summary>
		/// <value>
		/// 	<c>true</c> if this instance is disposed; otherwise, <c>false</c>.
		/// </value>
		public bool IsDisposed
		{
			get { return _disposed; }
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			Dispose(true);

			// Use SupressFinalize in case a subclass of this type implements a finalizer.
			GC.SuppressFinalize(this);
		}

        ~ApplicationEventsResolver()
		{
			// Run dispose but let the class know it was due to the finalizer running.
			Dispose(false);
		}

		private void Dispose(bool disposing)
		{
			// Only operate if we haven't already disposed
			if (IsDisposed || disposing == false) return;

			using (new WriteLock(_disposalLocker))
			{
				// Check again now we're inside the lock
				if (IsDisposed) return;

				// Call to actually release resources. This method is only
				// kept separate so that the entire disposal logic can be used as a VS snippet
				DisposeResources();

				// Indicate that the instance has been disposed.
				_disposed = true;
			}
		}

	    /// <summary>
	    /// Clear out all of the instances, we don't want them hanging around and cluttering up memory
	    /// </summary>
	    private void DisposeResources()
	    {
            _legacyResolver.Dispose();
            ResetCollections();
            _orderedAndFiltered.Clear();
	        _orderedAndFiltered = null;            
	    }
    }
}