// automatically generated by the FlatBuffers compiler, do not modify


#ifndef FLATBUFFERS_GENERATED_FRAME_HZMSG_H_
#define FLATBUFFERS_GENERATED_FRAME_HZMSG_H_

#include "flatbuffers/flatbuffers.h"

namespace HZMsg {

struct Scalar4;

struct Frame;

FLATBUFFERS_MANUALLY_ALIGNED_STRUCT(4) Scalar4 FLATBUFFERS_FINAL_CLASS {
 private:
  float x_;
  float y_;
  float z_;
  float w_;

 public:
  Scalar4() {
    memset(static_cast<void *>(this), 0, sizeof(Scalar4));
  }
  Scalar4(float _x, float _y, float _z, float _w)
      : x_(flatbuffers::EndianScalar(_x)),
        y_(flatbuffers::EndianScalar(_y)),
        z_(flatbuffers::EndianScalar(_z)),
        w_(flatbuffers::EndianScalar(_w)) {
  }
  float x() const {
    return flatbuffers::EndianScalar(x_);
  }
  void mutate_x(float _x) {
    flatbuffers::WriteScalar(&x_, _x);
  }
  float y() const {
    return flatbuffers::EndianScalar(y_);
  }
  void mutate_y(float _y) {
    flatbuffers::WriteScalar(&y_, _y);
  }
  float z() const {
    return flatbuffers::EndianScalar(z_);
  }
  void mutate_z(float _z) {
    flatbuffers::WriteScalar(&z_, _z);
  }
  float w() const {
    return flatbuffers::EndianScalar(w_);
  }
  void mutate_w(float _w) {
    flatbuffers::WriteScalar(&w_, _w);
  }
};
FLATBUFFERS_STRUCT_END(Scalar4, 16);

struct Frame FLATBUFFERS_FINAL_CLASS : private flatbuffers::Table {
  enum FlatBuffersVTableOffset FLATBUFFERS_VTABLE_UNDERLYING_TYPE {
    VT_N = 4,
    VT_I = 6,
    VT_POSITIONS = 8
  };
  int32_t N() const {
    return GetField<int32_t>(VT_N, 0);
  }
  bool mutate_N(int32_t _N) {
    return SetField<int32_t>(VT_N, _N, 0);
  }
  int32_t I() const {
    return GetField<int32_t>(VT_I, 0);
  }
  bool mutate_I(int32_t _I) {
    return SetField<int32_t>(VT_I, _I, 0);
  }
  const flatbuffers::Vector<const Scalar4 *> *positions() const {
    return GetPointer<const flatbuffers::Vector<const Scalar4 *> *>(VT_POSITIONS);
  }
  flatbuffers::Vector<const Scalar4 *> *mutable_positions() {
    return GetPointer<flatbuffers::Vector<const Scalar4 *> *>(VT_POSITIONS);
  }
  bool Verify(flatbuffers::Verifier &verifier) const {
    return VerifyTableStart(verifier) &&
           VerifyField<int32_t>(verifier, VT_N) &&
           VerifyField<int32_t>(verifier, VT_I) &&
           VerifyOffset(verifier, VT_POSITIONS) &&
           verifier.VerifyVector(positions()) &&
           verifier.EndTable();
  }
};

struct FrameBuilder {
  flatbuffers::FlatBufferBuilder &fbb_;
  flatbuffers::uoffset_t start_;
  void add_N(int32_t N) {
    fbb_.AddElement<int32_t>(Frame::VT_N, N, 0);
  }
  void add_I(int32_t I) {
    fbb_.AddElement<int32_t>(Frame::VT_I, I, 0);
  }
  void add_positions(flatbuffers::Offset<flatbuffers::Vector<const Scalar4 *>> positions) {
    fbb_.AddOffset(Frame::VT_POSITIONS, positions);
  }
  explicit FrameBuilder(flatbuffers::FlatBufferBuilder &_fbb)
        : fbb_(_fbb) {
    start_ = fbb_.StartTable();
  }
  FrameBuilder &operator=(const FrameBuilder &);
  flatbuffers::Offset<Frame> Finish() {
    const auto end = fbb_.EndTable(start_);
    auto o = flatbuffers::Offset<Frame>(end);
    return o;
  }
};

inline flatbuffers::Offset<Frame> CreateFrame(
    flatbuffers::FlatBufferBuilder &_fbb,
    int32_t N = 0,
    int32_t I = 0,
    flatbuffers::Offset<flatbuffers::Vector<const Scalar4 *>> positions = 0) {
  FrameBuilder builder_(_fbb);
  builder_.add_positions(positions);
  builder_.add_I(I);
  builder_.add_N(N);
  return builder_.Finish();
}

inline flatbuffers::Offset<Frame> CreateFrameDirect(
    flatbuffers::FlatBufferBuilder &_fbb,
    int32_t N = 0,
    int32_t I = 0,
    const std::vector<Scalar4> *positions = nullptr) {
  auto positions__ = positions ? _fbb.CreateVectorOfStructs<Scalar4>(*positions) : 0;
  return HZMsg::CreateFrame(
      _fbb,
      N,
      I,
      positions__);
}

inline const HZMsg::Frame *GetFrame(const void *buf) {
  return flatbuffers::GetRoot<HZMsg::Frame>(buf);
}

inline const HZMsg::Frame *GetSizePrefixedFrame(const void *buf) {
  return flatbuffers::GetSizePrefixedRoot<HZMsg::Frame>(buf);
}

inline Frame *GetMutableFrame(void *buf) {
  return flatbuffers::GetMutableRoot<Frame>(buf);
}

inline bool VerifyFrameBuffer(
    flatbuffers::Verifier &verifier) {
  return verifier.VerifyBuffer<HZMsg::Frame>(nullptr);
}

inline bool VerifySizePrefixedFrameBuffer(
    flatbuffers::Verifier &verifier) {
  return verifier.VerifySizePrefixedBuffer<HZMsg::Frame>(nullptr);
}

inline void FinishFrameBuffer(
    flatbuffers::FlatBufferBuilder &fbb,
    flatbuffers::Offset<HZMsg::Frame> root) {
  fbb.Finish(root);
}

inline void FinishSizePrefixedFrameBuffer(
    flatbuffers::FlatBufferBuilder &fbb,
    flatbuffers::Offset<HZMsg::Frame> root) {
  fbb.FinishSizePrefixed(root);
}

}  // namespace HZMsg

#endif  // FLATBUFFERS_GENERATED_FRAME_HZMSG_H_
