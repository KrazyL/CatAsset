﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace CatAsset
{
    /// <summary>
    /// 任务执行器
    /// </summary>
    public class TaskExcutor
    {
        /// <summary>
        /// 任务名称与对应Task
        /// </summary>
        private Dictionary<string, BaseTask> taskDict = new Dictionary<string, BaseTask>();

        /// <summary>
        /// 需要移除的任务
        /// </summary>
        private List<string> needRemoveTasks = new List<string>();

        /// <summary>
        /// 需要添加的任务
        /// </summary>
        private List<BaseTask> needAddTasks = new List<BaseTask>();

        /// <summary>
        /// 每帧最多执行任务次数
        /// </summary>
        public int MaxExcuteCount = 10;

        /// <summary>
        /// 是否存在指定任务
        /// </summary>
        public bool HasTask(string name)
        {
            return taskDict.ContainsKey(name);
        }

        /// <summary>
        /// 获取指定任务的状态
        /// </summary>
        public TaskState GetTaskState(string name)
        {
            return taskDict[name].State;
        }

        /// <summary>
        /// 追加任务执行回调
        /// </summary>
        public void AppendTaskCompleted(string name,Action<object> completed)
        {
            if (!taskDict.TryGetValue(name,out BaseTask task))
            {
                Debug.LogError("AppendTaskCompleted调用失败，此Task不存在：" + name);
                return;
            }

            task.Completed += completed;
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        public void AddTask(BaseTask task)
        {
            needAddTasks.Add(task);
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        private void InternalAddTask(BaseTask task)
        {
            if (HasTask(task.Name))
            {
                //任务已存在 不需要重复添加
                AppendTaskCompleted(task.Name, task.Completed);
                return;
            }

            taskDict.Add(task.Name, task);
        }

        /// <summary>
        /// 轮询任务
        /// </summary>
        public void Update()
        {
            if (needAddTasks.Count > 0)
            {
                //添加需要添加的任务
                for (int i = 0; i < needAddTasks.Count; i++)
                {
                    BaseTask task = needAddTasks[i];
                    InternalAddTask(task);
                }

                needAddTasks.Clear();
            }

            if (taskDict.Count > 0)
            {
                //处理任务
                int executeCount = 0;

                foreach (KeyValuePair<string, BaseTask> item in taskDict)
                {
                    if (executeCount >= MaxExcuteCount)
                    {
                        break;
                    }

                    BaseTask task = item.Value;

                    switch (task.State)
                    {
                        case TaskState.Free:
                            //Debug.Log("开始任务：" + task.Name);
                            task.Execute();
                            task.Update();
                            executeCount++;
                            break;
                        case TaskState.WaitOther:
                            //Debug.Log("等待任务：" + task.Name);
                            task.Update();
                            break;
                        case TaskState.Executing:
                            //Debug.Log("执行任务：" + task.Name);
                            task.Update();
                            executeCount++;
                            break;
                        case TaskState.Done:
                            //Debug.Log("完成任务：" + task.Name);
                            needRemoveTasks.Add(task.Name);
                            break;
                    }

                }
            }

            //移除需要移除的任务
            if (needRemoveTasks.Count > 0)
            {
                for (int i = 0; i < needRemoveTasks.Count; i++)
                {
                    string taskName = needRemoveTasks[i];
                    taskDict.Remove(taskName);
                }

                needRemoveTasks.Clear();
            }
        }
    }
}

