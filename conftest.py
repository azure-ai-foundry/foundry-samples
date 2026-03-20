# conftest.py
#
# Purpose
# -------
# Treat every Python sample under  doc-samples/agents/python/**
# as a Pytest test item.  The script "passes" if it runs without
# raising an exception (exit code 0).
#
# How it works
# ------------
# • pytest_collect_file() is a collection hook called for every file
#   Pytest sees.  We accept the file if:
#     – it ends with .py
#     – its path is under SAMPLE_ROOT (any depth)
# • Accepted files become SampleItem objects, which simply execute
#   the script with runpy.run_path().
#
# Edit SAMPLE_ROOT if you move the samples elsewhere.

import pathlib
import runpy
import pytest

# Root directory that contains all Python samples
SAMPLE_ROOT = (
    pathlib.Path(__file__).parent
    / "doc-samples"
    / "agents"
    / "python"
).resolve()


def _is_under_sample_root(path_obj: pathlib.Path) -> bool:
    """
    Return True if `path_obj` is inside SAMPLE_ROOT (recursive).

    Using Path.relative_to() is the safest way and works on all OSes.
    """
    try:
        path_obj.resolve().relative_to(SAMPLE_ROOT)
        return True
    except ValueError:
        return False


def pytest_collect_file(parent: pytest.Collector, file_path: pathlib.Path):
    """
    PyTest collection hook: decide whether *file_path* should become a test item.
    """
    if file_path.suffix == ".py" and _is_under_sample_root(file_path):
        return SampleItem.from_parent(parent, name=file_path.name, path=file_path)


class SampleItem(pytest.Item):
    """
    Wrapper around an arbitrary Python script.
    The test *passes* if the script finishes without an exception.
    """

    def runtest(self):
        # Execute the script in its own namespace.
        runpy.run_path(str(self.path))

    def repr_failure(self, excinfo):
        # Nicely format any exception raised during runtest().
        return f"Sample {self.path} failed:\n{excinfo.value}"

    def reportinfo(self):
        return self.path, 0, "sample script"
