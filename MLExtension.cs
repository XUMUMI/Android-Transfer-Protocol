using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Markup;

namespace MultiLanguageMarkup
{
    internal class LangExtension : MarkupExtension
    {
        private class target
        {
            public FrameworkElement elem;
            public PropertyInfo prop;
            public bool loaded;
        }


        // This will be updated by Multi-Language
        private static readonly string RootNamespace = "Android_Transfer_Protocol";     //MLHIDE

        // We only need one resource manager object. We can create it in a static constructor.
        private static readonly ResourceManager ResMgr;

        // The resource key will be provided as a parameter to the constructor,
        // using markup in the format
        // Text="{m:Lang MyResourceName}"
        private readonly string _ResourceKey;

        // If the markup extension is used in a data template, there may be multiple
        // FrameworkElement objects referring to a single MarkupExtension object.
        // To change the language at runtime, we must keep a list of all of them.
        private readonly List<target> _Targets;

        // Create the ResourceManager once only in the static constructor
        static LangExtension()
        {
            ResMgr = new ResourceManager(RootNamespace + ".Properties.Resources", Assembly.GetExecutingAssembly());
        }

        // Class constructor.
        // When you use markup in the format Text="{m:Lang MyResourceName}"
        // the constructor is called with the resource name as a parameter
        public LangExtension(string resourceKey)
        {
            _ResourceKey = resourceKey;
            _Targets = new List<target>();

#if false
      // Hook up to language changed event.
      MLRuntime.MLRuntime.LanguageChanged += ml_UpdateControls ;
#endif
        }

        //
        // ProvideValue is an abstract method in the base class MarkupExtension, which we
        // must implement to return the text contained in the resource string.
        //
        // We use this call to store a reference to the property, so that we can update it
        // when the language is changed at runtime. This is not possible in the constructor.
        //
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Save the FrameworkElement and the Property so that we can change the language on the fly.
            IProvideValueTarget provider = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            DependencyProperty dp = provider.TargetProperty as DependencyProperty;

            if (provider.TargetObject is FrameworkElement)
            {
                FrameworkElement element = provider.TargetObject as FrameworkElement;
                PropertyInfo propinfo = element.GetType().GetProperty(dp.Name);
                element.Unloaded += Element_Unloaded;
                element.Loaded += Element_Loaded;
                _Targets.Add(new target { elem = element, prop = propinfo, loaded = false });
            }
            else if (provider.TargetObject.GetType().FullName == "System.Windows.SharedDp")
            {
                // See
                // https://thomaslevesque.com/2009/08/23/wpf-markup-extensions-and-templates/
                //
                // By returning "this", we will be called again.
                // If the markup extension is used in a data template which is instantiated multiple times,
                // there will be a call for each instance. This means that we must build a list of targets.
                //
                return this;
            }

            // Now get the actual resource.
            return ResMgr.GetString(_ResourceKey);
        }

        protected virtual void ml_UpdateControls()
        {
            //
            // For a normal element, there will be one item in the list of targets.
            // For an element in a data template, there may be multiple items.
            //
            foreach (target t in _Targets)
            {
                if (t.loaded)
                {
                    if ((t.elem != null) && (t.prop != null))
                    {
                        string resString = ResMgr.GetString(_ResourceKey);
                        t.prop.SetValue(t.elem, resString);
                    }
                }
            }
        }

        //
        // If the markup extension is used in a data template, for example in a list box
        // item, the FrameworkElements may be added and deleted dynamically. When a
        // FrameworkElement is removed we should remove it from our list. Otherwise it
        // will prevent garbage collection from cleaning up these items and we might end
        // up trying to change the language on items which have long been deleted.
        //
        // (In simple cases, nothing bad will happen if we do not remove them, but
        // there will probably be some cases where it causes problems.)
        //
        private void Element_Unloaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                _Targets.Where(x => x.elem == element)
                        .ToList()
                        .ForEach(x => x.loaded = false);
            }
        }

        private void Element_Loaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                foreach (target x in _Targets)
                {
                    if ((x.elem == element) && (!x.loaded))
                    {
                        x.loaded = true;
                        // There might be a pending language chnge.
                        // Specifically if the element is on a TabItem which was hidden during a language change.
                        if (x.prop != null)
                        {
                            // Might be inefficient to get multiple times, but this is very unusual anyway.
                            string resString = ResMgr.GetString(_ResourceKey);
                            x.prop.SetValue(x.elem, resString);
                        }
                    }
                }
            }
        }

    }
}
