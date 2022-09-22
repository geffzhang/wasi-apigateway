using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wasmtime;
using Module = Wasmtime.Module;

namespace WasiApiGateway.Application
{
    public class WasiModulesRunner
    {
        public static async Task<string> Run(byte[] moduleContent, string message, CancellationToken cancellationToken)
        {
            
            using var engine = new Engine(); 
            using var module = Module.FromBytes(engine, "module", moduleContent);
            using var store = new Store(engine);
            using var linker = new Linker(engine);

            var stdinFilePath = Path.GetTempFileName();
            await File.WriteAllTextAsync(stdinFilePath, message, cancellationToken);

            var stdoutFilePath = Path.GetTempFileName();

            var stderrFilePath = Path.GetTempFileName();

            linker.DefineWasi();

            store.SetWasiConfiguration(new WasiConfiguration()
                .WithStandardInput(stdinFilePath)
                .WithStandardOutput(stdoutFilePath)
                .WithStandardError(stderrFilePath));
            
            using dynamic instance = linker.Instantiate(store, module);


            string reply = await await Task.WhenAny(
                Task.Run(async () =>
                {
                    instance.run();

                    string errorReply = await File.ReadAllTextAsync(stderrFilePath, cancellationToken);

                    if (errorReply is not "")
                    {
                        throw new ModuleRunningException(errorReply);
                    }

                    string successReply = await File.ReadAllTextAsync(stdoutFilePath, cancellationToken);
                    return successReply;
                }),
                WaitCancellation<string>(cancellationToken)
            );

            return reply;
        }

        private static async Task<T> WaitCancellation<T>(CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    public class ModuleRunningException : Exception
    {
        public ModuleRunningException(string message = null, Exception innerExecption = null) : base(message, innerExecption)
        {
            
        }
    }
}