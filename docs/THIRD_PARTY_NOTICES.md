# 로컬 음성 인식 및 번역 구성요소

현재 사용하는 직접 의존성과 모델의 출처 기록이다. 최종 배포 시 다른 전이 의존성의 고지도 함께 확인한다.

| 구성요소 | 출처 | 현재 사용 |
| --- | --- | --- |
| Whisper.net 및 CPU Runtime | https://github.com/sandrohanea/whisper.net | 1.9.1, C# 바인딩 및 whisper.cpp 네이티브 실행 |
| OpenAI Whisper | https://github.com/openai/whisper | 다국어 base 모델 |
| 모델 변환 파일 | https://huggingface.co/ggerganov/whisper.cpp | ggml-base.bin, 147951465 bytes |
| whisper.cpp | https://github.com/ggml-org/whisper.cpp | Whisper.net CPU 런타임에 포함된 엔진 |
| Ollama | https://github.com/ollama/ollama | 별도 설치한 로컬 모델 실행 도구, 0.34.0 |
| Qwen3 | https://ollama.com/library/qwen3 | 현재 8B 사용, 비교용 4B 보관. 모델 페이지의 Apache-2.0 라이선스 참조 |

모델 SHA-256: `60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe`

## Whisper.net

MIT License

Copyright (c) 2024 sandrohanea

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## OpenAI Whisper

MIT License

Copyright (c) 2022 OpenAI

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
