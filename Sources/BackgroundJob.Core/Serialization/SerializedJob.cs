using System;
using System.Linq;
using BackgroundJob.Host;

namespace BackgroundJob.Core.Serialization
{
    public class SerializedJob
    {
        public string Type { get; private set; }

        public string Method { get; private set; }

        public string ParameterTypes { get; private set; }

        public string Arguments { get; set; }

        public SerializedJob(string type, string method, string parameterTypes, string arguments)
        {
            Type = type;
            Method = method;
            ParameterTypes = parameterTypes;
            Arguments = arguments;
        }

        public IBackgroundJobDetail Deserialize()
        {
            try
            {
                var type = System.Type.GetType(Type, true, true);
                var types = JobHelper.FromJson<Type[]>(ParameterTypes);
                var method = type.GetMethod(Method, types);
                if (method == null)
                    throw new InvalidOperationException(
                        string.Format("The type `{0}` does not contain a method with signature `{1}({2})`",
                            type.FullName, Method,
                            string.Join(", ",
                                types.Select(x => x.Name))));
                var arguments = JobHelper.FromJson<string[]>(Arguments);
                return new BackgroundJobDetail(type, method, arguments);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Could not load the job. See inner exception for the details.", ex);
            }
        }

        internal static SerializedJob Serialize(BackgroundJobDetail job)
        {
            return new SerializedJob(job.Type.AssemblyQualifiedName, job.Method.Name,
                JobHelper.ToJson(
                    job.Method.GetParameters().Select(x => x.ParameterType)),
                JobHelper.ToJson(job.Arguments));
        }
    }
}