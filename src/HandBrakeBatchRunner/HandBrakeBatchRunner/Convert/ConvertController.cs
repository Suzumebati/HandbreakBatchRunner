﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HandBrakeBatchRunner.Convert
{
    /// <summary>
    /// アウトプットデータ受信イベント引数クラス
    /// </summary>
    public class OutputDataReceivedEventArgs : EventArgs
    {
        public int Progress;
        public string ConvertStatus;
        public string LogData;
    }

    /// <summary>
    /// 変換コントローラ
    /// </summary>
    public class ConvertController
    {
        /// <summary>
        /// アウトプットデータ受信イベントハンドラー
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void OutputDataReceivedHandler(object sender, OutputDataReceivedEventArgs e);

        /// <summary>
        /// アウトプットデータイベント
        /// </summary>
        public static event OutputDataReceivedHandler OutputDataReceivedEvent;

        /// <summary>
        /// キャンセルトークンソース
        /// </summary>
        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        /// <summary>
        /// 標準出力のエンドイベント
        /// </summary>
        private TaskCompletionSource<bool> outputEndEvent = new TaskCompletionSource<bool>();

        /// <summary>
        /// HandBrakeCLIのファイルパス
        /// </summary>
        public string HandBrakeCLIFilePath { get; set; }

        /// <summary>
        /// キャンセルフラグ
        /// </summary>
        public bool IsCancel
        {
            set
            {
                if (value)
                {
                    tokenSource.Cancel();
                }
            }
            get => tokenSource.IsCancellationRequested;
        }

        /// <summary>
        /// 完了フラグ
        /// </summary>
        public bool IsComplete { get; set; } = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="handBrakeCLIFilePath"></param>
        public ConvertController(string handBrakeCLIFilePath)
        {
            HandBrakeCLIFilePath = handBrakeCLIFilePath;
        }

        /// <summary>
        /// 変換実行
        /// </summary>
        /// <param name="convertSettingName"></param>
        /// <param name="srcFilePath"></param>
        /// <param name="dstFilePath"></param>
        public async Task ExecuteConvert(string convertSettingName, string srcFilePath, Dictionary<string, string> replaceParam)
        {
            // 変換設定を取得
            ConvertSettingItem setting = ConvertSettingManager.Current.GetSetting(convertSettingName);

            //Processオブジェクトを作成
            using (Process proc = new Process())
            {
                //出力をストリームに書き込むようにする
                ProcessStartInfo startInfo = proc.StartInfo;
                startInfo.FileName = HandBrakeCLIFilePath;
                startInfo.Arguments = setting.GetCommandLineParameter(replaceParam);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;

                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                //startInfo.StandardErrorEncoding = Encoding.UTF8;
                //startInfo.StandardOutputEncoding = Encoding.UTF8;

                proc.OutputDataReceived += new DataReceivedEventHandler(OutputDataReceived);
                proc.ErrorDataReceived += new DataReceivedEventHandler(OutputDataReceived);

                // 非同期実行開始
                ProcessResult result = await ExecuteConvertCommand(proc, 24 * 60 * 1000);
                IsComplete = result.Completed;
            }
        }

        /// <summary>
        /// プロセスを非同期に実行する
        /// </summary>
        /// <param name="proc"></param>
        /// <param name="timeout"></param>
        /// <param name="token"></param>
        /// <param name="outputCloseEvent"></param>
        /// <param name="errorCloseEvent"></param>
        /// <returns></returns>
        public async Task<ProcessResult> ExecuteConvertCommand(Process proc, int timeout)
        {
            ProcessResult result = new ProcessResult();
            bool isStarted;

            try
            {
                // プロセス実行開始
                isStarted = proc.Start();
            }
            catch (Exception error)
            {
                result.Completed = false;
                result.ExitCode = -1;
                result.ErrorMessage = error.Message;

                isStarted = false;
            }

            if (isStarted)
            {
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                int runTime = 0;
                const int waitTime = 1000;

                while (true)
                {
                    // 一定時間待つ
                    bool isExit = await WaitForExitAsync(proc, waitTime);
                    runTime += waitTime;

                    if (isExit || outputEndEvent.Task.IsCompleted)
                    {
                        // プロセスが完了した場合
                        result.Completed = true;
                        result.Canceled = false;
                        result.ExitCode = proc.ExitCode;
                        break;
                    }
                    else if (tokenSource.Token.IsCancellationRequested)
                    {
                        // キャンセルがされた場合
                        try
                        {
                            proc.Kill();
                        }
                        catch { }

                        result.Completed = false;
                        result.Canceled = true;
                        break;
                        //token.ThrowIfCancellationRequested();
                    }
                    else if (runTime > timeout)
                    {
                        // タイムアウトをオーバーした場合
                        try
                        {
                            proc.Kill();
                        }
                        catch { }

                        result.Completed = false;
                        result.Canceled = false;
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// プロセス完了を非同期に待つ
        /// </summary>
        /// <param name="process"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private Task<bool> WaitForExitAsync(Process process, int timeout)
        {
            return Task.Run(() => process.WaitForExit(timeout));
        }

        /// <summary>
        /// プロセス実行結果
        /// </summary>
        public class ProcessResult
        {
            /// <summary>
            /// 完了フラグ
            /// </summary>
            public bool Completed { get; set; } = false;

            /// <summary>
            /// キャンセルフラグ
            /// </summary>
            public bool Canceled { get; set; } = false;

            /// <summary>
            /// 終了コード
            /// </summary>
            public int? ExitCode { get; set; } = null;

            /// <summary>
            /// エラーメッセージ
            /// </summary>
            public string ErrorMessage { get; set; } = string.Empty;
        }

        /// <summary>
        /// プロセスからの標準出力・エラー出力を受け取る
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            // データがnullの場合はプロセスが終了した
            if (e == null)
            {
                outputEndEvent.SetResult(true);
                return;
            }

            // データが空の場合は無視
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            OutputDataReceivedEventArgs args = new OutputDataReceivedEventArgs
            {
                LogData = e.Data
            };

            // パーセンテージ＋各種情報
            if (Constant.LOG_PROGRESS_AND_TIME_REGEX.IsMatch(e.Data))
            {
                GroupCollection groups = Constant.LOG_PROGRESS_AND_TIME_REGEX.Match(e.Data).Groups;
                args.Progress = decimal.ToInt32(decimal.Round(decimal.Parse(groups[1].Value)));
                args.ConvertStatus = groups[2].Value;
            }
            // パーセンテージのみ
            if (Constant.LOG_PROGRESS_REGEX.IsMatch(e.Data))
            {
                GroupCollection groups = Constant.LOG_PROGRESS_REGEX.Match(e.Data).Groups;
                args.Progress = decimal.ToInt32(decimal.Round(decimal.Parse(groups[1].Value)));
            }
            // イベントを発行
            OnOutputDataReceived(args);
        }

        /// <summary>
        /// イベントを発生させる
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnOutputDataReceived(OutputDataReceivedEventArgs e)
        {
            OutputDataReceivedEvent?.Invoke(this, e);
        }

    }
}