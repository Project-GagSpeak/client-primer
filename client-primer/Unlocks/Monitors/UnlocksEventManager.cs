using GagSpeak.Achievements;
#nullable disable
public class UnlocksEventManager
{
    private static Dictionary<UnlocksEvent, Delegate> EventDictionary = new Dictionary<UnlocksEvent, Delegate>();

    // Subscribe with no parameters
    public void Subscribe(UnlocksEvent eventName, Action listener)
    {
        if (!EventDictionary.ContainsKey(eventName))
        {
            EventDictionary[eventName] = null;
        }
        EventDictionary[eventName] = (Action)EventDictionary[eventName] + listener;
    }

    // Subscribe with one parameter
    public void Subscribe<T>(UnlocksEvent eventName, Action<T> listener)
    {
        if (!EventDictionary.ContainsKey(eventName))
        {
            EventDictionary[eventName] = null;
        }
        EventDictionary[eventName] = (Action<T>)EventDictionary[eventName] + listener;
    }

    // Subscribe with two parameters
    public void Subscribe<T1, T2>(UnlocksEvent eventName, Action<T1, T2> listener)
    {
        if (!EventDictionary.ContainsKey(eventName))
        {
            EventDictionary[eventName] = null;
        }
        EventDictionary[eventName] = (Action<T1, T2>)EventDictionary[eventName] + listener;
    }

    // Subscribe with three parameters
    public void Subscribe<T1, T2, T3>(UnlocksEvent eventName, Action<T1, T2, T3> listener)
    {
        if (!EventDictionary.ContainsKey(eventName))
        {
            EventDictionary[eventName] = null;
        }
        EventDictionary[eventName] = (Action<T1, T2, T3>)EventDictionary[eventName] + listener;
    }

    // Subscribe with four parameters
    public void Subscribe<T1, T2, T3, T4>(UnlocksEvent eventName, Action<T1, T2, T3, T4> listener)
    {
        if (!EventDictionary.ContainsKey(eventName))
        {
            EventDictionary[eventName] = null;
        }
        EventDictionary[eventName] = (Action<T1, T2, T3, T4>)EventDictionary[eventName] + listener;
    }

    // Unsubscribe with no parameters
    public void Unsubscribe(UnlocksEvent eventName, Action listener)
    {
        if (EventDictionary.TryGetValue(eventName, out var existingDelegate))
        {
            var currentListeners = (Action)existingDelegate;
            currentListeners -= listener;
            if (currentListeners == null)
            {
                EventDictionary.Remove(eventName);
            }
            else
            {
                EventDictionary[eventName] = currentListeners;
            }
        }
    }

    // Unsubscribe with one parameter
    public void Unsubscribe<T>(UnlocksEvent eventName, Action<T> listener)
    {
        if (EventDictionary.TryGetValue(eventName, out var existingDelegate))
        {
            var currentListeners = (Action<T>)existingDelegate;
            currentListeners -= listener;
            if (currentListeners == null)
            {
                EventDictionary.Remove(eventName);
            }
            else
            {
                EventDictionary[eventName] = currentListeners;
            }
        }
    }

    // Unsubscribe with two parameters
    public void Unsubscribe<T1, T2>(UnlocksEvent eventName, Action<T1, T2> listener)
    {
        if (EventDictionary.TryGetValue(eventName, out var existingDelegate))
        {
            var currentListeners = (Action<T1, T2>)existingDelegate;
            currentListeners -= listener;
            if (currentListeners == null)
            {
                EventDictionary.Remove(eventName);
            }
            else
            {
                EventDictionary[eventName] = currentListeners;
            }
        }
    }

    // Unsubscribe with three parameters
    public void Unsubscribe<T1, T2, T3>(UnlocksEvent eventName, Action<T1, T2, T3> listener)
    {
        if (EventDictionary.TryGetValue(eventName, out var existingDelegate))
        {
            var currentListeners = (Action<T1, T2, T3>)existingDelegate;
            currentListeners -= listener;
            if (currentListeners == null)
            {
                EventDictionary.Remove(eventName);
            }
            else
            {
                EventDictionary[eventName] = currentListeners;
            }
        }
    }

    // Unsubscribe with four parameters
    public void Unsubscribe<T1, T2, T3, T4>(UnlocksEvent eventName, Action<T1, T2, T3, T4> listener)
    {
        if (EventDictionary.TryGetValue(eventName, out var existingDelegate))
        {
            var currentListeners = (Action<T1, T2, T3, T4>)existingDelegate;
            currentListeners -= listener;
            if (currentListeners == null)
            {
                EventDictionary.Remove(eventName);
            }
            else
            {
                EventDictionary[eventName] = currentListeners;
            }
        }
    }

    // Trigger event with no parameter
    public static void TriggerEvent(UnlocksEvent eventName)
    {
        if (EventDictionary.TryGetValue(eventName, out var action))
        {
            var eventHandler = (Action)action;
            eventHandler?.Invoke();
        }
    }

    // Trigger event with one parameter
    public static void TriggerEvent<T>(UnlocksEvent eventName, T param)
    {
        if (EventDictionary.TryGetValue(eventName, out var action))
        {
            var eventHandler = (Action<T>)action;
            eventHandler?.Invoke(param);
        }
    }

    // Trigger event with two parameters
    public void TriggerEvent<T1, T2>(UnlocksEvent eventName, T1 param1, T2 param2)
    {
        if (EventDictionary.TryGetValue(eventName, out var action))
        {
            var eventHandler = (Action<T1, T2>)action;
            eventHandler?.Invoke(param1, param2);
        }
    }

    // Trigger event with three parameters
    public void TriggerEvent<T1, T2, T3>(UnlocksEvent eventName, T1 param1, T2 param2, T3 param3)
    {
        if (EventDictionary.TryGetValue(eventName, out var action))
        {
            var eventHandler = (Action<T1, T2, T3>)action;
            eventHandler?.Invoke(param1, param2, param3);
        }
    }

    // Trigger event with four parameters
    public void TriggerEvent<T1, T2, T3, T4>(UnlocksEvent eventName, T1 param1, T2 param2, T3 param3, T4 param4)
    {
        if (EventDictionary.TryGetValue(eventName, out var action))
        {
            var eventHandler = (Action<T1, T2, T3, T4>)action;
            eventHandler?.Invoke(param1, param2, param3, param4);
        }
    }
}
