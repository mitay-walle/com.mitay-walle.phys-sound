using UnityEngine;

namespace PhysSound.Utilities
{
    public struct TimeTrigger
    {
        float _endTime;
        public float Time;

        public TimeTrigger(float time)
        {
            this.Time = time;
            _endTime = 0;
        }

        public bool CheckAndRestart()
        {
            bool isComplete = IsReady();
            if (isComplete)
                Restart();
            return isComplete;
        }

        public bool CheckAndCancel()
        {
            bool isComplete = IsReady();
            if (isComplete)
                _endTime = float.MaxValue;
            return isComplete;
        }

        public bool IsReady()
        {
            return UnityEngine.Time.time >= _endTime;
        }

        public void Restart()
        {
            _endTime = UnityEngine.Time.time + Time;
        }

        public void Restart(float time)
        {
            this.Time = time;
            _endTime = UnityEngine.Time.time + time;
        }

        public void Reset()
        {
            _endTime = UnityEngine.Time.time;
        }

        public void Reset(float time)
        {
            _endTime = UnityEngine.Time.time + time;
        }

        public float GetTimeLeft()
        {
            return Mathf.Max(0, _endTime - UnityEngine.Time.time);
        }
    
        public float GetTimeElapsed()
        {
            return Mathf.Max(0, _endTime - GetTimeLeft());
        }

        public float GetStartTime()
        {
            return _endTime - Time;
        }
    }
}
