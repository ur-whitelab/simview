# automatically generated by the FlatBuffers compiler, do not modify

# namespace: HZMsg

import flatbuffers

class Frame(object):
    __slots__ = ['_tab']

    @classmethod
    def GetRootAsFrame(cls, buf, offset):
        n = flatbuffers.encode.Get(flatbuffers.packer.uoffset, buf, offset)
        x = Frame()
        x.Init(buf, n + offset)
        return x

    # Frame
    def Init(self, buf, pos):
        self._tab = flatbuffers.table.Table(buf, pos)

    # Frame
    def N(self):
        o = flatbuffers.number_types.UOffsetTFlags.py_type(self._tab.Offset(4))
        if o != 0:
            return self._tab.Get(flatbuffers.number_types.Int32Flags, o + self._tab.Pos)
        return 0

    # Frame
    def I(self):
        o = flatbuffers.number_types.UOffsetTFlags.py_type(self._tab.Offset(6))
        if o != 0:
            return self._tab.Get(flatbuffers.number_types.Int32Flags, o + self._tab.Pos)
        return 0

    # Frame
    def Time(self):
        o = flatbuffers.number_types.UOffsetTFlags.py_type(self._tab.Offset(8))
        if o != 0:
            return self._tab.Get(flatbuffers.number_types.Int32Flags, o + self._tab.Pos)
        return 0

    # Frame
    def Positions(self, j):
        o = flatbuffers.number_types.UOffsetTFlags.py_type(self._tab.Offset(10))
        if o != 0:
            x = self._tab.Vector(o)
            x += flatbuffers.number_types.UOffsetTFlags.py_type(j) * 16
            from .Scalar4 import Scalar4
            obj = Scalar4()
            obj.Init(self._tab.Bytes, x)
            return obj
        return None

    # Frame
    def PositionsLength(self):
        o = flatbuffers.number_types.UOffsetTFlags.py_type(self._tab.Offset(10))
        if o != 0:
            return self._tab.VectorLen(o)
        return 0

def FrameStart(builder): builder.StartObject(4)
def FrameAddN(builder, N): builder.PrependInt32Slot(0, N, 0)
def FrameAddI(builder, I): builder.PrependInt32Slot(1, I, 0)
def FrameAddTime(builder, time): builder.PrependInt32Slot(2, time, 0)
def FrameAddPositions(builder, positions): builder.PrependUOffsetTRelativeSlot(3, flatbuffers.number_types.UOffsetTFlags.py_type(positions), 0)
def FrameStartPositionsVector(builder, numElems): return builder.StartVector(16, numElems, 4)
def FrameEnd(builder): return builder.EndObject()
