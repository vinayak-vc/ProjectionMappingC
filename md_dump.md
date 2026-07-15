
A module that was compiled using NumPy 1.x cannot be run in
NumPy 2.4.4 as it may crash. To support both 1.x and 2.x
versions of NumPy, modules must be compiled with NumPy 2.0.
Some module may need to rebuild instead e.g. with 'pybind11>=2.12'.

If you are a user of the module, the easiest solution will be to
downgrade to 'numpy<2' or try to upgrade the affected module.
We expect that some modules will need time to support NumPy 2.

Traceback (most recent call last):  File "<frozen runpy>", line 189, in _run_module_as_main
  File "<frozen runpy>", line 148, in _get_module_details
  File "<frozen runpy>", line 112, in _get_module_details
  File "C:\Python311\Lib\site-packages\markitdown\__init__.py", line 6, in <module>
    from ._markitdown import (
  File "C:\Python311\Lib\site-packages\markitdown\_markitdown.py", line 15, in <module>
    import magika
  File "C:\Python311\Lib\site-packages\magika\__init__.py", line 23, in <module>
    from magika.magika import Magika
  File "C:\Python311\Lib\site-packages\magika\magika.py", line 31, in <module>
    import onnxruntime as rt
  File "C:\Python311\Lib\site-packages\onnxruntime\__init__.py", line 23, in <module>
    from onnxruntime.capi._pybind_state import ExecutionMode  # noqa: F401
  File "C:\Python311\Lib\site-packages\onnxruntime\capi\_pybind_state.py", line 32, in <module>
    from .onnxruntime_pybind11_state import *  # noqa
AttributeError: _ARRAY_API not found
Traceback (most recent call last):
  File "<frozen runpy>", line 189, in _run_module_as_main
  File "<frozen runpy>", line 148, in _get_module_details
  File "<frozen runpy>", line 112, in _get_module_details
  File "C:\Python311\Lib\site-packages\markitdown\__init__.py", line 6, in <module>
    from ._markitdown import (
  File "C:\Python311\Lib\site-packages\markitdown\_markitdown.py", line 15, in <module>
    import magika
  File "C:\Python311\Lib\site-packages\magika\__init__.py", line 23, in <module>
    from magika.magika import Magika
  File "C:\Python311\Lib\site-packages\magika\magika.py", line 31, in <module>
    import onnxruntime as rt
  File "C:\Python311\Lib\site-packages\onnxruntime\__init__.py", line 57, in <module>
    raise import_capi_exception
  File "C:\Python311\Lib\site-packages\onnxruntime\__init__.py", line 23, in <module>
    from onnxruntime.capi._pybind_state import ExecutionMode  # noqa: F401
    ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
  File "C:\Python311\Lib\site-packages\onnxruntime\capi\_pybind_state.py", line 32, in <module>
    from .onnxruntime_pybind11_state import *  # noqa
    ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
ImportError
