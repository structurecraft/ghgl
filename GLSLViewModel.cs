﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rhino.Geometry;

namespace ghgl
{
    class GLSLViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        const double DEFAULT_LINE_WIDTH = 3.0;
        const double DEFAULT_POINT_SIZE = 8.0;

        readonly Shader[] _shaders = new Shader[5];
        bool _compileFailed = false;
        uint _programId;
        double _glLineWidth = DEFAULT_LINE_WIDTH;
        double _glPointSize = DEFAULT_POINT_SIZE;
        uint _drawMode;
        DateTime _startTime = DateTime.Now;

        public GLSLViewModel()
        {
            for (int i = 0; i < 5; i++)
                _shaders[i] = new Shader((ShaderType)i, this);
        }

        void SetCode(int which, string v, [System.Runtime.CompilerServices.CallerMemberName] string memberName = null)
        {
            if (!string.Equals(_shaders[which].Code, v, StringComparison.Ordinal))
            {
                _shaders[which].Code = v;
                OnPropertyChanged(memberName);
            }
        }

        public string VertexShaderCode
        {
            get { return _shaders[(int)ShaderType.Vertex].Code; }
            set { SetCode((int)ShaderType.Vertex, value); }
        }
        public string TessellationControlCode
        {
            get { return _shaders[(int)ShaderType.TessellationControl].Code; }
            set { SetCode((int)ShaderType.TessellationControl, value); }
        }
        public string TessellationEvalualtionCode
        {
            get { return _shaders[(int)ShaderType.TessellationEval].Code; }
            set { SetCode((int)ShaderType.TessellationEval, value); }
        }
        public string FragmentShaderCode
        {
            get { return _shaders[(int)ShaderType.Fragment].Code; }
            set { SetCode((int)ShaderType.Fragment, value); }
        }
        public string GeometryShaderCode
        {
            get { return _shaders[(int)ShaderType.Geometry].Code; }
            set { SetCode((int)ShaderType.Geometry, value); }
        }

        public uint ProgramId
        {
            get { return _programId; }
            set
            {
                if (_programId != value)
                {
                    GLRecycleBin.AddProgramToDeleteList(_programId);
                    _programId = value;
                    OnPropertyChanged();
                }
            }
        }

        public double glLineWidth
        {
            get { return _glLineWidth; }
            set
            {
                if (_glLineWidth != value && value > 0)
                {
                    _glLineWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public double glPointSize
        {
            get { return _glPointSize; }
            set
            {
                if (_glPointSize != value && value > 0)
                {
                    _glPointSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public uint DrawMode
        {
            get { return _drawMode; }
            set
            {
                if (_drawMode != value && _drawMode <= OpenGL.GL_PATCHES)
                {
                    _drawMode = value;
                    OnPropertyChanged();
                }
            }
        }

        void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string memberName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(memberName));

            if (memberName == "VertexShaderCode" || memberName == "TessellationControlCode" || memberName == "TessellationEvalualtionCode"
              || memberName == "FragmentShaderCode" || memberName == "GeometryShaderCode")
            {
                ProgramId = 0;
                _compileFailed = false;
            }
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;



        public bool CompileProgram(List<string> errors)
        {
            if (ProgramId != 0)
                return true;
            if (_compileFailed)
                return false;

            GLRecycleBin.Recycle();

            bool compileSuccess = true;
            foreach (var shader in _shaders)
                compileSuccess = shader.Compile(errors) && compileSuccess;

            // we want to make sure that at least a vertex and fragment shader
            // exist before making a program
            if (string.IsNullOrWhiteSpace(_shaders[(int)ShaderType.Vertex].Code))
            {
                errors.Add("A vertex shader is required to create a GL program");
                compileSuccess = false;
            }
            if (string.IsNullOrWhiteSpace(_shaders[(int)ShaderType.Fragment].Code))
            {
                errors.Add("A fragment shader is required to create a GL program");
                compileSuccess = false;
            }

            if (compileSuccess)
            {
                ProgramId = OpenGL.glCreateProgram();
                foreach (var shader in _shaders)
                {
                    if (shader.ShaderId != 0)
                        OpenGL.glAttachShader(ProgramId, shader.ShaderId);
                }

                OpenGL.glLinkProgram(ProgramId);

                string errorMsg;
                if (OpenGL.ErrorOccurred(out errorMsg))
                {
                    OpenGL.glDeleteProgram(_programId);
                    ProgramId = 0;
                    errors.Add(errorMsg);
                }
            }
            _compileFailed = (ProgramId == 0);
            return ProgramId != 0;
        }

        public bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetString("VertexShader", VertexShaderCode);
            writer.SetString("GeometryShader", GeometryShaderCode);
            writer.SetString("FragmentShader", FragmentShaderCode);
            writer.SetString("TessCtrlShader", TessellationControlCode);
            writer.SetString("TessEvalShader", TessellationEvalualtionCode);
            writer.SetDouble("glLineWidth", glLineWidth);
            writer.SetDouble("glPointSize", glPointSize);
            writer.SetInt32("DrawMode", (int)DrawMode);
            return true;
        }

        public bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            string s = "";
            if (reader.TryGetString("VertexShader", ref s))
                VertexShaderCode = s;
            else
                VertexShaderCode = "";

            if (reader.TryGetString("GeometryShader", ref s))
                GeometryShaderCode = s;
            else
                GeometryShaderCode = "";

            if (reader.TryGetString("FragmentShader", ref s))
                FragmentShaderCode = s;
            else
                FragmentShaderCode = "";

            if (reader.TryGetString("TessCtrlShader", ref s))
                TessellationControlCode = s;
            else
                TessellationControlCode = "";

            if (reader.TryGetString("TessEvalShader", ref s))
                TessellationEvalualtionCode = s;
            else
                TessellationEvalualtionCode = "";

            double d = 0;
            if (reader.TryGetDouble("glLineWidth", ref d))
                glLineWidth = d;
            if (reader.TryGetDouble("glPointSize", ref d))
                glPointSize = d;
            int i = 0;
            if (reader.TryGetInt32("DrawMode", ref i))
                DrawMode = (uint)i;
            return true;
        }

        /// <summary>
        /// Get the data type for a uniform in this program (all shaders)
        /// </summary>
        /// <param name="name">name of uniform to try and get a type for</param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        public bool TryGetUniformType(string name, out string dataType)
        {
            dataType = "";
            foreach (var shader in _shaders)
            {
                var uniforms = shader.GetUniforms();
                foreach (UniformDescription uni in uniforms)
                {
                    if (uni.Name == name)
                    {
                        dataType = uni.DataType;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool TryGetAttributeType(string name, out string dataType, out int location)
        {
            dataType = "";
            location = -1;
            foreach (var shader in _shaders)
            {
                var attributes = shader.GetVertexAttributes();
                foreach (AttributeDescription attrib in attributes)
                {
                    if (attrib.Name == name)
                    {
                        dataType = attrib.DataType;
                        location = attrib.Location;
                        return true;
                    }
                }
            }
            return false;
        }


        class UniformData<T>
        {
            public UniformData(string name, T value)
            {
                Name = name;
                Data = value;
            }

            public string Name { get; private set; }
            public T Data { get; private set; }
        }

        class SamplerUniformData
        {
            uint _textureId;

            public SamplerUniformData(string name, string path)
            {
                Name = name;
                Path = path;
            }
            public string Name { get; private set; }
            public String Path { get; private set; }

            public uint TextureId
            {
                get { return _textureId; }
                set
                {
                    if (_textureId != value)
                    {
                        GLRecycleBin.AddTextureToDeleteList(_textureId);
                        _textureId = value;
                    }
                }
            }
            public static uint CreateTexture(string path)
            {
                uint textureId = 0;
                try
                {
                    using (var bmp = new System.Drawing.Bitmap(path))
                    {
                        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
                        uint[] textures = { 0 };
                        OpenGL.glGenTextures(1, textures);
                        OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, textures[0]);

                        if (bmp.PixelFormat == System.Drawing.Imaging.PixelFormat.Format24bppRgb)
                        {
                            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                            OpenGL.glTexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGB, bmpData.Width, bmpData.Height, 0, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, bmpData.Scan0);
                            bmp.UnlockBits(bmpData);
                        }
                        else
                        {
                            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            OpenGL.glTexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGBA, bmpData.Width, bmpData.Height, 0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, bmpData.Scan0);
                            bmp.UnlockBits(bmpData);
                        }
                        textureId = textures[0];
                        OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, (int)OpenGL.GL_CLAMP);
                        OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, (int)OpenGL.GL_CLAMP);
                        OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, (int)OpenGL.GL_LINEAR);
                        OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, (int)OpenGL.GL_LINEAR);
                        OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, 0);
                    }
                }
                catch (Exception)
                {
                    textureId = 0;
                }

                return textureId;
            }
        }

        List<UniformData<int>> _intUniforms = new List<UniformData<int>>();
        List<UniformData<float>> _floatUniforms = new List<UniformData<float>>();
        List<UniformData<Rhino.Geometry.Point3f>> _vec3Uniforms = new List<UniformData<Rhino.Geometry.Point3f>>();
        List<UniformData<Vec4>> _vec4Uniforms = new List<UniformData<Vec4>>();
        List<SamplerUniformData> _sampler2DUniforms = new List<SamplerUniformData>();

        public void AddUniform(string name, int value)
        {
            _intUniforms.Add(new UniformData<int>(name, value));
        }
        public void AddUniform(string name, float value)
        {
            _floatUniforms.Add(new UniformData<float>(name, value));
        }
        public void AddUniform(string name, Rhino.Geometry.Point3f value)
        {
            _vec3Uniforms.Add(new UniformData<Rhino.Geometry.Point3f>(name, value));
        }
        public void AddUniform(string name, Vec4 value)
        {
            _vec4Uniforms.Add(new UniformData<Vec4>(name, value));
        }
        public void AddSampler2DUniform(string name, string path)
        {
            var data = new SamplerUniformData(name, path);
            //try to find a cached item first
            for (int i = 0; i < samplerCache.Count; i++)
            {
                var sampler = samplerCache[i];
                if (string.Equals(sampler.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    data.TextureId = sampler.TextureId;
                    samplerCache.RemoveAt(i);
                    break;
                }
            }
            _sampler2DUniforms.Add(data);
        }

        public void AddAttribute(string name, int location, int[] value)
        {
            _intAttribs.Add(new GLAttribute<int>(name, location, value));
        }
        public void AddAttribute(string name, int location, float[] value)
        {
            _floatAttribs.Add(new GLAttribute<float>(name, location, value));
        }
        public void AddAttribute(string name, int location, Rhino.Geometry.Point3f[] value)
        {
            _vec3Attribs.Add(new GLAttribute<Rhino.Geometry.Point3f>(name, location, value));
        }
        public void AddAttribute(string name, int location, Vec4[] value)
        {
            _vec4Attribs.Add(new GLAttribute<Vec4>(name, location, value));
        }

        void SetupGLUniforms()
        {
            foreach (var uniform in _intUniforms)
            {
                int location = OpenGL.glGetUniformLocation(ProgramId, uniform.Name);
                if (-1 != location)
                    OpenGL.glUniform1i(location, uniform.Data);
            }
            foreach (var uniform in _floatUniforms)
            {
                int location = OpenGL.glGetUniformLocation(ProgramId, uniform.Name);
                if (-1 != location)
                    OpenGL.glUniform1f(location, uniform.Data);
            }
            foreach (var uniform in _vec3Uniforms)
            {
                int location = OpenGL.glGetUniformLocation(ProgramId, uniform.Name);
                if (-1 != location)
                    OpenGL.glUniform3f(location, uniform.Data.X, uniform.Data.Y, uniform.Data.Z);
            }
            foreach (var uniform in _vec4Uniforms)
            {
                int location = OpenGL.glGetUniformLocation(ProgramId, uniform.Name);
                if (-1 != location)
                    OpenGL.glUniform4f(location, uniform.Data._x, uniform.Data._y, uniform.Data._z, uniform.Data._w);
            }

            int currentTexture = 0;
            foreach (var uniform in _sampler2DUniforms)
            {
                int location = OpenGL.glGetUniformLocation(ProgramId, uniform.Name);
                if (-1 != location)
                {
                    if (0 == uniform.TextureId)
                    {
                        uniform.TextureId = SamplerUniformData.CreateTexture(uniform.Path);
                    }
                    if (uniform.TextureId != 0)
                    {
                        OpenGL.glActiveTexture(OpenGL.GL_TEXTURE0 + (uint)currentTexture);
                        OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, uniform.TextureId);
                        OpenGL.glUniform1i(location, currentTexture);
                        currentTexture++;
                    }
                }
            }
        }

        int SetupGLAttributes()
        {
            int element_count = 0;
            foreach (var item in _intAttribs)
            {
                if (element_count == 0)
                    element_count = item.Items.Length;
                if (element_count > item.Items.Length && item.Items.Length > 1)
                    element_count = item.Items.Length;

                if (item.Location < 0)
                {
                    item.Location = OpenGL.glGetAttribLocation(ProgramId, item.Name);
                }
                if (item.Location >= 0)
                {
                    uint location = (uint)item.Location;
                    if (1 == item.Items.Length)
                    {
                        OpenGL.glDisableVertexAttribArray(location);
                        OpenGL.glVertexAttribI1i(location, item.Items[0]);
                    }
                    else
                    {
                        if (item.VboHandle == 0)
                        {
                            uint[] buffers;
                            OpenGL.glGenBuffers(1, out buffers);
                            item.VboHandle = buffers[0];
                            OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                            IntPtr size = new IntPtr(sizeof(int) * item.Items.Length);
                            var handle = GCHandle.Alloc(item.Items, GCHandleType.Pinned);
                            IntPtr pointer = handle.AddrOfPinnedObject();
                            OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                            handle.Free();
                        }
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                        OpenGL.glEnableVertexAttribArray(location);
                        OpenGL.glVertexAttribPointer(location, 1, OpenGL.GL_INT, 0, sizeof(int), IntPtr.Zero);
                    }
                }
            }
            foreach (var item in _floatAttribs)
            {
                if (element_count == 0)
                    element_count = item.Items.Length;
                if (element_count > item.Items.Length && item.Items.Length > 1)
                    element_count = item.Items.Length;

                if (item.Location < 0)
                {
                    item.Location = OpenGL.glGetAttribLocation(ProgramId, item.Name);
                }
                if (item.Location >= 0)
                {
                    uint location = (uint)item.Location;
                    if (1 == item.Items.Length)
                    {
                        OpenGL.glDisableVertexAttribArray(location);
                        OpenGL.glVertexAttrib1f(location, item.Items[0]);
                    }
                    else
                    {
                        if (item.VboHandle == 0)
                        {
                            uint[] buffers;
                            OpenGL.glGenBuffers(1, out buffers);
                            item.VboHandle = buffers[0];
                            OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                            IntPtr size = new IntPtr(sizeof(float) * item.Items.Length);
                            var handle = GCHandle.Alloc(item.Items, GCHandleType.Pinned);
                            IntPtr pointer = handle.AddrOfPinnedObject();
                            OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                            handle.Free();
                        }
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                        OpenGL.glEnableVertexAttribArray(location);
                        OpenGL.glVertexAttribPointer(location, 1, OpenGL.GL_FLOAT, 0, sizeof(float), IntPtr.Zero);
                    }
                }
            }
            foreach (var item in _vec3Attribs)
            {
                if (element_count == 0)
                    element_count = item.Items.Length;
                if (element_count > item.Items.Length && item.Items.Length > 1)
                    element_count = item.Items.Length;

                if (item.Location < 0)
                {
                    item.Location = OpenGL.glGetAttribLocation(ProgramId, item.Name);
                }
                if (item.Location >= 0)
                {
                    uint location = (uint)item.Location;
                    if (1 == item.Items.Length)
                    {
                        OpenGL.glDisableVertexAttribArray(location);
                        Point3f v = item.Items[0];
                        OpenGL.glVertexAttrib3f(location, v.X, v.Y, v.Z);
                    }
                    else
                    {
                        if (item.VboHandle == 0)
                        {
                            uint[] buffers;
                            OpenGL.glGenBuffers(1, out buffers);
                            item.VboHandle = buffers[0];
                            OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                            IntPtr size = new IntPtr(3 * sizeof(float) * item.Items.Length);
                            var handle = GCHandle.Alloc(item.Items, GCHandleType.Pinned);
                            IntPtr pointer = handle.AddrOfPinnedObject();
                            OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                            handle.Free();
                        }
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                        OpenGL.glEnableVertexAttribArray(location);
                        OpenGL.glVertexAttribPointer(location, 3, OpenGL.GL_FLOAT, 0, 3 * sizeof(float), IntPtr.Zero);
                    }
                }
            }
            foreach (var item in _vec4Attribs)
            {
                if (element_count == 0)
                    element_count = item.Items.Length;
                if (element_count > item.Items.Length && item.Items.Length > 1)
                    element_count = item.Items.Length;

                if (item.Location < 0)
                {
                    item.Location = OpenGL.glGetAttribLocation(ProgramId, item.Name);
                }
                if (item.Location >= 0)
                {
                    uint location = (uint)item.Location;
                    if (1 == item.Items.Length)
                    {
                        OpenGL.glDisableVertexAttribArray(location);
                        Vec4 v = item.Items[0];
                        OpenGL.glVertexAttrib4f(location, v._x, v._y, v._z, v._w);
                    }
                    else
                    {
                        if (item.VboHandle == 0)
                        {
                            uint[] buffers;
                            OpenGL.glGenBuffers(1, out buffers);
                            item.VboHandle = buffers[0];
                            OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                            IntPtr size = new IntPtr(4 * sizeof(float) * item.Items.Length);
                            var handle = GCHandle.Alloc(item.Items, GCHandleType.Pinned);
                            IntPtr pointer = handle.AddrOfPinnedObject();
                            OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                            handle.Free();
                        }
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                        OpenGL.glEnableVertexAttribArray(location);
                        OpenGL.glVertexAttribPointer(location, 4, OpenGL.GL_FLOAT, 0, 4 * sizeof(float), IntPtr.Zero);
                    }
                }
            }
            return element_count;
        }

        readonly List<GLAttribute<int>> _intAttribs = new List<GLAttribute<int>>();
        readonly List<GLAttribute<float>> _floatAttribs = new List<GLAttribute<float>>();
        readonly List<GLAttribute<Point3f>> _vec3Attribs = new List<GLAttribute<Point3f>>();
        readonly List<GLAttribute<Vec4>> _vec4Attribs = new List<GLAttribute<Vec4>>();

        readonly List<SamplerUniformData> samplerCache = new List<SamplerUniformData>();

        public void ClearData()
        {
            _intUniforms.Clear();
            _floatUniforms.Clear();
            _vec3Uniforms.Clear();
            _vec4Uniforms.Clear();

            foreach (var attr in _intAttribs)
                GLRecycleBin.AddVboToDeleteList(attr.VboHandle);
            _intAttribs.Clear();
            foreach (var attr in _floatAttribs)
                GLRecycleBin.AddVboToDeleteList(attr.VboHandle);
            _floatAttribs.Clear();
            foreach (var attr in _vec3Attribs)
                GLRecycleBin.AddVboToDeleteList(attr.VboHandle);
            _vec3Attribs.Clear();
            foreach (var attr in _vec4Attribs)
                GLRecycleBin.AddVboToDeleteList(attr.VboHandle);
            _vec4Attribs.Clear();

            samplerCache.AddRange(_sampler2DUniforms);
            while (samplerCache.Count > 10)
            {
                var sampler = samplerCache[0];
                GLRecycleBin.AddTextureToDeleteList(sampler.TextureId);
                samplerCache.RemoveAt(0);
            }
            _sampler2DUniforms.Clear();
        }

        public void Draw(Rhino.Display.DisplayPipeline display)
        {
            uint programId = ProgramId;
            if (programId == 0)
                return;

            uint[] vao;
            OpenGL.glGenVertexArrays(1, out vao);
            OpenGL.glBindVertexArray(vao[0]);
            OpenGL.glUseProgram(programId);

            SetupGLUniforms();
            int element_count = SetupGLAttributes();


            // TODO: Parse shader and figure out the proper number to place here
            if (OpenGL.GL_PATCHES == DrawMode)
                OpenGL.glPatchParameteri(OpenGL.GL_PATCH_VERTICES, 1);

            float linewidth = (float)glLineWidth;
            OpenGL.glLineWidth(linewidth);
            float pointsize = (float)glPointSize;
            OpenGL.glPointSize(pointsize);

            // Define standard uniforms
            int uniformLocation = -1;
            uniformLocation = OpenGL.glGetUniformLocation(programId, "_viewportSize");
            if (uniformLocation >= 0)
            {
                var viewportSize = display.Viewport.Size;
                OpenGL.glUniform2f(uniformLocation, (float)viewportSize.Width, (float)viewportSize.Height);
            }
            uniformLocation = OpenGL.glGetUniformLocation(programId, "_worldToClip");
            if (uniformLocation >= 0)
            {
                float[] w2c = display.GetOpenGLWorldToClip(true);
                OpenGL.glUniformMatrix4fv(uniformLocation, 1, false, w2c);
            }
            uniformLocation = OpenGL.glGetUniformLocation(programId, "_time");
            if (uniformLocation >= 0)
            {
                var span = DateTime.Now - _startTime;
                double seconds = span.TotalSeconds;
                OpenGL.glUniform1f(uniformLocation, (float)seconds);
            }


            if (element_count > 0)
            {
                if (OpenGL.GL_POINTS == DrawMode)
                    OpenGL.glEnable(OpenGL.GL_VERTEX_PROGRAM_POINT_SIZE);
                OpenGL.glEnable(OpenGL.GL_BLEND);
                OpenGL.glBlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

                OpenGL.glDrawArrays(DrawMode, 0, element_count);
            }

            foreach (var item in _intAttribs)
                DisableVertexAttribArray(item.Location);
            foreach (var item in _floatAttribs)
                DisableVertexAttribArray(item.Location);
            foreach (var item in _vec3Attribs)
                DisableVertexAttribArray(item.Location);
            foreach (var item in _vec4Attribs)
                DisableVertexAttribArray(item.Location);

            OpenGL.glBindVertexArray(0);
            OpenGL.glDeleteVertexArrays(1, vao);
            OpenGL.glUseProgram(0);
        }

        static void DisableVertexAttribArray(int location)
        {
            if (location >= 0)
                OpenGL.glDisableVertexAttribArray((uint)location);
        }
    }
}
