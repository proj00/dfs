// @ts-nocheck
/**
 * Generated by the protoc-gen-ts.  DO NOT EDIT!
 * compiler version: 3.20.3
 * source: rpc/uiservice.proto
 * git: https://github.com/thesayyn/protoc-gen-ts */
import * as dependency_1 from "./..\\fs\\filesystem";
import * as dependency_2 from "./..\\rpc_common";
import * as pb_1 from "google-protobuf";
import * as grpc_1 from "@grpc/grpc-js";
export namespace Ui {
  export class ObjectOptions extends pb_1.Message {
    #one_of_decls: number[][] = [];
    constructor(
      data?:
        | any[]
        | {
            pickFolder?: boolean;
          },
    ) {
      super();
      pb_1.Message.initialize(
        this,
        Array.isArray(data) ? data : [],
        0,
        -1,
        [],
        this.#one_of_decls,
      );
      if (!Array.isArray(data) && typeof data == "object") {
        if ("pickFolder" in data && data.pickFolder != undefined) {
          this.pickFolder = data.pickFolder;
        }
      }
    }
    get pickFolder() {
      return pb_1.Message.getFieldWithDefault(this, 1, false) as boolean;
    }
    set pickFolder(value: boolean) {
      pb_1.Message.setField(this, 1, value);
    }
    static fromObject(data: { pickFolder?: boolean }): ObjectOptions {
      const message = new ObjectOptions({});
      if (data.pickFolder != null) {
        message.pickFolder = data.pickFolder;
      }
      return message;
    }
    toObject() {
      const data: {
        pickFolder?: boolean;
      } = {};
      if (this.pickFolder != null) {
        data.pickFolder = this.pickFolder;
      }
      return data;
    }
    serialize(): Uint8Array;
    serialize(w: pb_1.BinaryWriter): void;
    serialize(w?: pb_1.BinaryWriter): Uint8Array | void {
      const writer = w || new pb_1.BinaryWriter();
      if (this.pickFolder != false) writer.writeBool(1, this.pickFolder);
      if (!w) return writer.getResultBuffer();
    }
    static deserialize(bytes: Uint8Array | pb_1.BinaryReader): ObjectOptions {
      const reader =
          bytes instanceof pb_1.BinaryReader
            ? bytes
            : new pb_1.BinaryReader(bytes),
        message = new ObjectOptions();
      while (reader.nextField()) {
        if (reader.isEndGroup()) break;
        switch (reader.getFieldNumber()) {
          case 1:
            message.pickFolder = reader.readBool();
            break;
          default:
            reader.skipField();
        }
      }
      return message;
    }
    serializeBinary(): Uint8Array {
      return this.serialize();
    }
    static deserializeBinary(bytes: Uint8Array): ObjectOptions {
      return ObjectOptions.deserialize(bytes);
    }
  }
  export class Progress extends pb_1.Message {
    #one_of_decls: number[][] = [];
    constructor(
      data?:
        | any[]
        | {
            current?: number;
            total?: number;
          },
    ) {
      super();
      pb_1.Message.initialize(
        this,
        Array.isArray(data) ? data : [],
        0,
        -1,
        [],
        this.#one_of_decls,
      );
      if (!Array.isArray(data) && typeof data == "object") {
        if ("current" in data && data.current != undefined) {
          this.current = data.current;
        }
        if ("total" in data && data.total != undefined) {
          this.total = data.total;
        }
      }
    }
    get current() {
      return pb_1.Message.getFieldWithDefault(this, 1, 0) as number;
    }
    set current(value: number) {
      pb_1.Message.setField(this, 1, value);
    }
    get total() {
      return pb_1.Message.getFieldWithDefault(this, 2, 0) as number;
    }
    set total(value: number) {
      pb_1.Message.setField(this, 2, value);
    }
    static fromObject(data: { current?: number; total?: number }): Progress {
      const message = new Progress({});
      if (data.current != null) {
        message.current = data.current;
      }
      if (data.total != null) {
        message.total = data.total;
      }
      return message;
    }
    toObject() {
      const data: {
        current?: number;
        total?: number;
      } = {};
      if (this.current != null) {
        data.current = this.current;
      }
      if (this.total != null) {
        data.total = this.total;
      }
      return data;
    }
    serialize(): Uint8Array;
    serialize(w: pb_1.BinaryWriter): void;
    serialize(w?: pb_1.BinaryWriter): Uint8Array | void {
      const writer = w || new pb_1.BinaryWriter();
      if (this.current != 0) writer.writeInt64(1, this.current);
      if (this.total != 0) writer.writeInt64(2, this.total);
      if (!w) return writer.getResultBuffer();
    }
    static deserialize(bytes: Uint8Array | pb_1.BinaryReader): Progress {
      const reader =
          bytes instanceof pb_1.BinaryReader
            ? bytes
            : new pb_1.BinaryReader(bytes),
        message = new Progress();
      while (reader.nextField()) {
        if (reader.isEndGroup()) break;
        switch (reader.getFieldNumber()) {
          case 1:
            message.current = reader.readInt64();
            break;
          case 2:
            message.total = reader.readInt64();
            break;
          default:
            reader.skipField();
        }
      }
      return message;
    }
    serializeBinary(): Uint8Array {
      return this.serialize();
    }
    static deserializeBinary(bytes: Uint8Array): Progress {
      return Progress.deserialize(bytes);
    }
  }
  export class Path extends pb_1.Message {
    #one_of_decls: number[][] = [];
    constructor(
      data?:
        | any[]
        | {
            path?: string;
          },
    ) {
      super();
      pb_1.Message.initialize(
        this,
        Array.isArray(data) ? data : [],
        0,
        -1,
        [],
        this.#one_of_decls,
      );
      if (!Array.isArray(data) && typeof data == "object") {
        if ("path" in data && data.path != undefined) {
          this.path = data.path;
        }
      }
    }
    get path() {
      return pb_1.Message.getFieldWithDefault(this, 1, "") as string;
    }
    set path(value: string) {
      pb_1.Message.setField(this, 1, value);
    }
    static fromObject(data: { path?: string }): Path {
      const message = new Path({});
      if (data.path != null) {
        message.path = data.path;
      }
      return message;
    }
    toObject() {
      const data: {
        path?: string;
      } = {};
      if (this.path != null) {
        data.path = this.path;
      }
      return data;
    }
    serialize(): Uint8Array;
    serialize(w: pb_1.BinaryWriter): void;
    serialize(w?: pb_1.BinaryWriter): Uint8Array | void {
      const writer = w || new pb_1.BinaryWriter();
      if (this.path.length) writer.writeString(1, this.path);
      if (!w) return writer.getResultBuffer();
    }
    static deserialize(bytes: Uint8Array | pb_1.BinaryReader): Path {
      const reader =
          bytes instanceof pb_1.BinaryReader
            ? bytes
            : new pb_1.BinaryReader(bytes),
        message = new Path();
      while (reader.nextField()) {
        if (reader.isEndGroup()) break;
        switch (reader.getFieldNumber()) {
          case 1:
            message.path = reader.readString();
            break;
          default:
            reader.skipField();
        }
      }
      return message;
    }
    serializeBinary(): Uint8Array {
      return this.serialize();
    }
    static deserializeBinary(bytes: Uint8Array): Path {
      return Path.deserialize(bytes);
    }
  }
  export class String extends pb_1.Message {
    #one_of_decls: number[][] = [];
    constructor(
      data?:
        | any[]
        | {
            value?: string;
          },
    ) {
      super();
      pb_1.Message.initialize(
        this,
        Array.isArray(data) ? data : [],
        0,
        -1,
        [],
        this.#one_of_decls,
      );
      if (!Array.isArray(data) && typeof data == "object") {
        if ("value" in data && data.value != undefined) {
          this.value = data.value;
        }
      }
    }
    get value() {
      return pb_1.Message.getFieldWithDefault(this, 1, "") as string;
    }
    set value(value: string) {
      pb_1.Message.setField(this, 1, value);
    }
    static fromObject(data: { value?: string }): String {
      const message = new String({});
      if (data.value != null) {
        message.value = data.value;
      }
      return message;
    }
    toObject() {
      const data: {
        value?: string;
      } = {};
      if (this.value != null) {
        data.value = this.value;
      }
      return data;
    }
    serialize(): Uint8Array;
    serialize(w: pb_1.BinaryWriter): void;
    serialize(w?: pb_1.BinaryWriter): Uint8Array | void {
      const writer = w || new pb_1.BinaryWriter();
      if (this.value.length) writer.writeString(1, this.value);
      if (!w) return writer.getResultBuffer();
    }
    static deserialize(bytes: Uint8Array | pb_1.BinaryReader): String {
      const reader =
          bytes instanceof pb_1.BinaryReader
            ? bytes
            : new pb_1.BinaryReader(bytes),
        message = new String();
      while (reader.nextField()) {
        if (reader.isEndGroup()) break;
        switch (reader.getFieldNumber()) {
          case 1:
            message.value = reader.readString();
            break;
          default:
            reader.skipField();
        }
      }
      return message;
    }
    serializeBinary(): Uint8Array {
      return this.serialize();
    }
    static deserializeBinary(bytes: Uint8Array): String {
      return String.deserialize(bytes);
    }
  }
  export class ObjectFromDiskOptions extends pb_1.Message {
    #one_of_decls: number[][] = [];
    constructor(
      data?:
        | any[]
        | {
            path?: string;
            chunkSize?: number;
          },
    ) {
      super();
      pb_1.Message.initialize(
        this,
        Array.isArray(data) ? data : [],
        0,
        -1,
        [],
        this.#one_of_decls,
      );
      if (!Array.isArray(data) && typeof data == "object") {
        if ("path" in data && data.path != undefined) {
          this.path = data.path;
        }
        if ("chunkSize" in data && data.chunkSize != undefined) {
          this.chunkSize = data.chunkSize;
        }
      }
    }
    get path() {
      return pb_1.Message.getFieldWithDefault(this, 1, "") as string;
    }
    set path(value: string) {
      pb_1.Message.setField(this, 1, value);
    }
    get chunkSize() {
      return pb_1.Message.getFieldWithDefault(this, 2, 0) as number;
    }
    set chunkSize(value: number) {
      pb_1.Message.setField(this, 2, value);
    }
    static fromObject(data: {
      path?: string;
      chunkSize?: number;
    }): ObjectFromDiskOptions {
      const message = new ObjectFromDiskOptions({});
      if (data.path != null) {
        message.path = data.path;
      }
      if (data.chunkSize != null) {
        message.chunkSize = data.chunkSize;
      }
      return message;
    }
    toObject() {
      const data: {
        path?: string;
        chunkSize?: number;
      } = {};
      if (this.path != null) {
        data.path = this.path;
      }
      if (this.chunkSize != null) {
        data.chunkSize = this.chunkSize;
      }
      return data;
    }
    serialize(): Uint8Array;
    serialize(w: pb_1.BinaryWriter): void;
    serialize(w?: pb_1.BinaryWriter): Uint8Array | void {
      const writer = w || new pb_1.BinaryWriter();
      if (this.path.length) writer.writeString(1, this.path);
      if (this.chunkSize != 0) writer.writeInt32(2, this.chunkSize);
      if (!w) return writer.getResultBuffer();
    }
    static deserialize(
      bytes: Uint8Array | pb_1.BinaryReader,
    ): ObjectFromDiskOptions {
      const reader =
          bytes instanceof pb_1.BinaryReader
            ? bytes
            : new pb_1.BinaryReader(bytes),
        message = new ObjectFromDiskOptions();
      while (reader.nextField()) {
        if (reader.isEndGroup()) break;
        switch (reader.getFieldNumber()) {
          case 1:
            message.path = reader.readString();
            break;
          case 2:
            message.chunkSize = reader.readInt32();
            break;
          default:
            reader.skipField();
        }
      }
      return message;
    }
    serializeBinary(): Uint8Array {
      return this.serialize();
    }
    static deserializeBinary(bytes: Uint8Array): ObjectFromDiskOptions {
      return ObjectFromDiskOptions.deserialize(bytes);
    }
  }
  export class PublishingOptions extends pb_1.Message {
    #one_of_decls: number[][] = [];
    constructor(
      data?:
        | any[]
        | {
            containerGuid?: string;
            trackerUri?: string;
          },
    ) {
      super();
      pb_1.Message.initialize(
        this,
        Array.isArray(data) ? data : [],
        0,
        -1,
        [],
        this.#one_of_decls,
      );
      if (!Array.isArray(data) && typeof data == "object") {
        if ("containerGuid" in data && data.containerGuid != undefined) {
          this.containerGuid = data.containerGuid;
        }
        if ("trackerUri" in data && data.trackerUri != undefined) {
          this.trackerUri = data.trackerUri;
        }
      }
    }
    get containerGuid() {
      return pb_1.Message.getFieldWithDefault(this, 1, "") as string;
    }
    set containerGuid(value: string) {
      pb_1.Message.setField(this, 1, value);
    }
    get trackerUri() {
      return pb_1.Message.getFieldWithDefault(this, 2, "") as string;
    }
    set trackerUri(value: string) {
      pb_1.Message.setField(this, 2, value);
    }
    static fromObject(data: {
      containerGuid?: string;
      trackerUri?: string;
    }): PublishingOptions {
      const message = new PublishingOptions({});
      if (data.containerGuid != null) {
        message.containerGuid = data.containerGuid;
      }
      if (data.trackerUri != null) {
        message.trackerUri = data.trackerUri;
      }
      return message;
    }
    toObject() {
      const data: {
        containerGuid?: string;
        trackerUri?: string;
      } = {};
      if (this.containerGuid != null) {
        data.containerGuid = this.containerGuid;
      }
      if (this.trackerUri != null) {
        data.trackerUri = this.trackerUri;
      }
      return data;
    }
    serialize(): Uint8Array;
    serialize(w: pb_1.BinaryWriter): void;
    serialize(w?: pb_1.BinaryWriter): Uint8Array | void {
      const writer = w || new pb_1.BinaryWriter();
      if (this.containerGuid.length) writer.writeString(1, this.containerGuid);
      if (this.trackerUri.length) writer.writeString(2, this.trackerUri);
      if (!w) return writer.getResultBuffer();
    }
    static deserialize(
      bytes: Uint8Array | pb_1.BinaryReader,
    ): PublishingOptions {
      const reader =
          bytes instanceof pb_1.BinaryReader
            ? bytes
            : new pb_1.BinaryReader(bytes),
        message = new PublishingOptions();
      while (reader.nextField()) {
        if (reader.isEndGroup()) break;
        switch (reader.getFieldNumber()) {
          case 1:
            message.containerGuid = reader.readString();
            break;
          case 2:
            message.trackerUri = reader.readString();
            break;
          default:
            reader.skipField();
        }
      }
      return message;
    }
    serializeBinary(): Uint8Array {
      return this.serialize();
    }
    static deserializeBinary(bytes: Uint8Array): PublishingOptions {
      return PublishingOptions.deserialize(bytes);
    }
  }
  export class DownloadContainerOptions extends pb_1.Message {
    #one_of_decls: number[][] = [];
    constructor(
      data?:
        | any[]
        | {
            containerGuid?: string;
            trackerUri?: string;
            destinationDir?: string;
            maxConcurrentChunks?: number;
          },
    ) {
      super();
      pb_1.Message.initialize(
        this,
        Array.isArray(data) ? data : [],
        0,
        -1,
        [],
        this.#one_of_decls,
      );
      if (!Array.isArray(data) && typeof data == "object") {
        if ("containerGuid" in data && data.containerGuid != undefined) {
          this.containerGuid = data.containerGuid;
        }
        if ("trackerUri" in data && data.trackerUri != undefined) {
          this.trackerUri = data.trackerUri;
        }
        if ("destinationDir" in data && data.destinationDir != undefined) {
          this.destinationDir = data.destinationDir;
        }
        if (
          "maxConcurrentChunks" in data &&
          data.maxConcurrentChunks != undefined
        ) {
          this.maxConcurrentChunks = data.maxConcurrentChunks;
        }
      }
    }
    get containerGuid() {
      return pb_1.Message.getFieldWithDefault(this, 1, "") as string;
    }
    set containerGuid(value: string) {
      pb_1.Message.setField(this, 1, value);
    }
    get trackerUri() {
      return pb_1.Message.getFieldWithDefault(this, 2, "") as string;
    }
    set trackerUri(value: string) {
      pb_1.Message.setField(this, 2, value);
    }
    get destinationDir() {
      return pb_1.Message.getFieldWithDefault(this, 3, "") as string;
    }
    set destinationDir(value: string) {
      pb_1.Message.setField(this, 3, value);
    }
    get maxConcurrentChunks() {
      return pb_1.Message.getFieldWithDefault(this, 4, 0) as number;
    }
    set maxConcurrentChunks(value: number) {
      pb_1.Message.setField(this, 4, value);
    }
    static fromObject(data: {
      containerGuid?: string;
      trackerUri?: string;
      destinationDir?: string;
      maxConcurrentChunks?: number;
    }): DownloadContainerOptions {
      const message = new DownloadContainerOptions({});
      if (data.containerGuid != null) {
        message.containerGuid = data.containerGuid;
      }
      if (data.trackerUri != null) {
        message.trackerUri = data.trackerUri;
      }
      if (data.destinationDir != null) {
        message.destinationDir = data.destinationDir;
      }
      if (data.maxConcurrentChunks != null) {
        message.maxConcurrentChunks = data.maxConcurrentChunks;
      }
      return message;
    }
    toObject() {
      const data: {
        containerGuid?: string;
        trackerUri?: string;
        destinationDir?: string;
        maxConcurrentChunks?: number;
      } = {};
      if (this.containerGuid != null) {
        data.containerGuid = this.containerGuid;
      }
      if (this.trackerUri != null) {
        data.trackerUri = this.trackerUri;
      }
      if (this.destinationDir != null) {
        data.destinationDir = this.destinationDir;
      }
      if (this.maxConcurrentChunks != null) {
        data.maxConcurrentChunks = this.maxConcurrentChunks;
      }
      return data;
    }
    serialize(): Uint8Array;
    serialize(w: pb_1.BinaryWriter): void;
    serialize(w?: pb_1.BinaryWriter): Uint8Array | void {
      const writer = w || new pb_1.BinaryWriter();
      if (this.containerGuid.length) writer.writeString(1, this.containerGuid);
      if (this.trackerUri.length) writer.writeString(2, this.trackerUri);
      if (this.destinationDir.length)
        writer.writeString(3, this.destinationDir);
      if (this.maxConcurrentChunks != 0)
        writer.writeInt32(4, this.maxConcurrentChunks);
      if (!w) return writer.getResultBuffer();
    }
    static deserialize(
      bytes: Uint8Array | pb_1.BinaryReader,
    ): DownloadContainerOptions {
      const reader =
          bytes instanceof pb_1.BinaryReader
            ? bytes
            : new pb_1.BinaryReader(bytes),
        message = new DownloadContainerOptions();
      while (reader.nextField()) {
        if (reader.isEndGroup()) break;
        switch (reader.getFieldNumber()) {
          case 1:
            message.containerGuid = reader.readString();
            break;
          case 2:
            message.trackerUri = reader.readString();
            break;
          case 3:
            message.destinationDir = reader.readString();
            break;
          case 4:
            message.maxConcurrentChunks = reader.readInt32();
            break;
          default:
            reader.skipField();
        }
      }
      return message;
    }
    serializeBinary(): Uint8Array {
      return this.serialize();
    }
    static deserializeBinary(bytes: Uint8Array): DownloadContainerOptions {
      return DownloadContainerOptions.deserialize(bytes);
    }
  }
  interface GrpcUnaryServiceInterface<P, R> {
    (
      message: P,
      metadata: grpc_1.Metadata,
      options: grpc_1.CallOptions,
      callback: grpc_1.requestCallback<R>,
    ): grpc_1.ClientUnaryCall;
    (
      message: P,
      metadata: grpc_1.Metadata,
      callback: grpc_1.requestCallback<R>,
    ): grpc_1.ClientUnaryCall;
    (
      message: P,
      options: grpc_1.CallOptions,
      callback: grpc_1.requestCallback<R>,
    ): grpc_1.ClientUnaryCall;
    (message: P, callback: grpc_1.requestCallback<R>): grpc_1.ClientUnaryCall;
  }
  interface GrpcStreamServiceInterface<P, R> {
    (
      message: P,
      metadata: grpc_1.Metadata,
      options?: grpc_1.CallOptions,
    ): grpc_1.ClientReadableStream<R>;
    (message: P, options?: grpc_1.CallOptions): grpc_1.ClientReadableStream<R>;
  }
  interface GrpWritableServiceInterface<P, R> {
    (
      metadata: grpc_1.Metadata,
      options: grpc_1.CallOptions,
      callback: grpc_1.requestCallback<R>,
    ): grpc_1.ClientWritableStream<P>;
    (
      metadata: grpc_1.Metadata,
      callback: grpc_1.requestCallback<R>,
    ): grpc_1.ClientWritableStream<P>;
    (
      options: grpc_1.CallOptions,
      callback: grpc_1.requestCallback<R>,
    ): grpc_1.ClientWritableStream<P>;
    (callback: grpc_1.requestCallback<R>): grpc_1.ClientWritableStream<P>;
  }
  interface GrpcChunkServiceInterface<P, R> {
    (
      metadata: grpc_1.Metadata,
      options?: grpc_1.CallOptions,
    ): grpc_1.ClientDuplexStream<P, R>;
    (options?: grpc_1.CallOptions): grpc_1.ClientDuplexStream<P, R>;
  }
  interface GrpcPromiseServiceInterface<P, R> {
    (
      message: P,
      metadata: grpc_1.Metadata,
      options?: grpc_1.CallOptions,
    ): Promise<R>;
    (message: P, options?: grpc_1.CallOptions): Promise<R>;
  }
  export abstract class UnimplementedUiService {
    static definition = {
      PickObjectPath: {
        path: "/Ui.Ui/PickObjectPath",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: ObjectOptions) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          ObjectOptions.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: Path) => Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          Path.deserialize(new Uint8Array(bytes)),
      },
      GetObjectPath: {
        path: "/Ui.Ui/GetObjectPath",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: dependency_2.rpc_common.Hash) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Hash.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: Path) => Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          Path.deserialize(new Uint8Array(bytes)),
      },
      RevealObjectInExplorer: {
        path: "/Ui.Ui/RevealObjectInExplorer",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: dependency_2.rpc_common.Hash) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Hash.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: dependency_2.rpc_common.Empty) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Empty.deserialize(new Uint8Array(bytes)),
      },
      GetAllContainers: {
        path: "/Ui.Ui/GetAllContainers",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: dependency_2.rpc_common.Empty) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Empty.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: dependency_2.rpc_common.GuidList) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.GuidList.deserialize(new Uint8Array(bytes)),
      },
      GetDownloadProgress: {
        path: "/Ui.Ui/GetDownloadProgress",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: dependency_2.rpc_common.Hash) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Hash.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: Progress) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          Progress.deserialize(new Uint8Array(bytes)),
      },
      GetContainerObjects: {
        path: "/Ui.Ui/GetContainerObjects",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: dependency_2.rpc_common.Guid) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Guid.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: dependency_1.fs.ObjectList) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          dependency_1.fs.ObjectList.deserialize(new Uint8Array(bytes)),
      },
      GetContainerRootHash: {
        path: "/Ui.Ui/GetContainerRootHash",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: dependency_2.rpc_common.Guid) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Guid.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: dependency_2.rpc_common.Hash) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Hash.deserialize(new Uint8Array(bytes)),
      },
      ImportObjectFromDisk: {
        path: "/Ui.Ui/ImportObjectFromDisk",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: ObjectFromDiskOptions) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          ObjectFromDiskOptions.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: dependency_2.rpc_common.Guid) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Guid.deserialize(new Uint8Array(bytes)),
      },
      PublishToTracker: {
        path: "/Ui.Ui/PublishToTracker",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: PublishingOptions) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          PublishingOptions.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: dependency_2.rpc_common.Empty) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Empty.deserialize(new Uint8Array(bytes)),
      },
      DownloadContainer: {
        path: "/Ui.Ui/DownloadContainer",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: DownloadContainerOptions) =>
          Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          DownloadContainerOptions.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: dependency_2.rpc_common.Empty) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Empty.deserialize(new Uint8Array(bytes)),
      },
      CopyToClipboard: {
        path: "/Ui.Ui/CopyToClipboard",
        requestStream: false,
        responseStream: false,
        requestSerialize: (message: String) => Buffer.from(message.serialize()),
        requestDeserialize: (bytes: Buffer) =>
          String.deserialize(new Uint8Array(bytes)),
        responseSerialize: (message: dependency_2.rpc_common.Empty) =>
          Buffer.from(message.serialize()),
        responseDeserialize: (bytes: Buffer) =>
          dependency_2.rpc_common.Empty.deserialize(new Uint8Array(bytes)),
      },
    };
    [method: string]: grpc_1.UntypedHandleCall;
    abstract PickObjectPath(
      call: grpc_1.ServerUnaryCall<ObjectOptions, Path>,
      callback: grpc_1.sendUnaryData<Path>,
    ): void;
    abstract GetObjectPath(
      call: grpc_1.ServerUnaryCall<dependency_2.rpc_common.Hash, Path>,
      callback: grpc_1.sendUnaryData<Path>,
    ): void;
    abstract RevealObjectInExplorer(
      call: grpc_1.ServerUnaryCall<
        dependency_2.rpc_common.Hash,
        dependency_2.rpc_common.Empty
      >,
      callback: grpc_1.sendUnaryData<dependency_2.rpc_common.Empty>,
    ): void;
    abstract GetAllContainers(
      call: grpc_1.ServerUnaryCall<
        dependency_2.rpc_common.Empty,
        dependency_2.rpc_common.GuidList
      >,
      callback: grpc_1.sendUnaryData<dependency_2.rpc_common.GuidList>,
    ): void;
    abstract GetDownloadProgress(
      call: grpc_1.ServerUnaryCall<dependency_2.rpc_common.Hash, Progress>,
      callback: grpc_1.sendUnaryData<Progress>,
    ): void;
    abstract GetContainerObjects(
      call: grpc_1.ServerUnaryCall<
        dependency_2.rpc_common.Guid,
        dependency_1.fs.ObjectList
      >,
      callback: grpc_1.sendUnaryData<dependency_1.fs.ObjectList>,
    ): void;
    abstract GetContainerRootHash(
      call: grpc_1.ServerUnaryCall<
        dependency_2.rpc_common.Guid,
        dependency_2.rpc_common.Hash
      >,
      callback: grpc_1.sendUnaryData<dependency_2.rpc_common.Hash>,
    ): void;
    abstract ImportObjectFromDisk(
      call: grpc_1.ServerUnaryCall<
        ObjectFromDiskOptions,
        dependency_2.rpc_common.Guid
      >,
      callback: grpc_1.sendUnaryData<dependency_2.rpc_common.Guid>,
    ): void;
    abstract PublishToTracker(
      call: grpc_1.ServerUnaryCall<
        PublishingOptions,
        dependency_2.rpc_common.Empty
      >,
      callback: grpc_1.sendUnaryData<dependency_2.rpc_common.Empty>,
    ): void;
    abstract DownloadContainer(
      call: grpc_1.ServerUnaryCall<
        DownloadContainerOptions,
        dependency_2.rpc_common.Empty
      >,
      callback: grpc_1.sendUnaryData<dependency_2.rpc_common.Empty>,
    ): void;
    abstract CopyToClipboard(
      call: grpc_1.ServerUnaryCall<String, dependency_2.rpc_common.Empty>,
      callback: grpc_1.sendUnaryData<dependency_2.rpc_common.Empty>,
    ): void;
  }
  export class UiClient extends grpc_1.makeGenericClientConstructor(
    UnimplementedUiService.definition,
    "Ui",
    {},
  ) {
    constructor(
      address: string,
      credentials: grpc_1.ChannelCredentials,
      options?: Partial<grpc_1.ChannelOptions>,
    ) {
      super(address, credentials, options);
    }
    PickObjectPath: GrpcUnaryServiceInterface<ObjectOptions, Path> = (
      message: ObjectOptions,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<Path>,
      options?: grpc_1.CallOptions | grpc_1.requestCallback<Path>,
      callback?: grpc_1.requestCallback<Path>,
    ): grpc_1.ClientUnaryCall => {
      return super.PickObjectPath(message, metadata, options, callback);
    };
    GetObjectPath: GrpcUnaryServiceInterface<
      dependency_2.rpc_common.Hash,
      Path
    > = (
      message: dependency_2.rpc_common.Hash,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<Path>,
      options?: grpc_1.CallOptions | grpc_1.requestCallback<Path>,
      callback?: grpc_1.requestCallback<Path>,
    ): grpc_1.ClientUnaryCall => {
      return super.GetObjectPath(message, metadata, options, callback);
    };
    RevealObjectInExplorer: GrpcUnaryServiceInterface<
      dependency_2.rpc_common.Hash,
      dependency_2.rpc_common.Empty
    > = (
      message: dependency_2.rpc_common.Hash,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
      options?:
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
      callback?: grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
    ): grpc_1.ClientUnaryCall => {
      return super.RevealObjectInExplorer(message, metadata, options, callback);
    };
    GetAllContainers: GrpcUnaryServiceInterface<
      dependency_2.rpc_common.Empty,
      dependency_2.rpc_common.GuidList
    > = (
      message: dependency_2.rpc_common.Empty,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.GuidList>,
      options?:
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.GuidList>,
      callback?: grpc_1.requestCallback<dependency_2.rpc_common.GuidList>,
    ): grpc_1.ClientUnaryCall => {
      return super.GetAllContainers(message, metadata, options, callback);
    };
    GetDownloadProgress: GrpcUnaryServiceInterface<
      dependency_2.rpc_common.Hash,
      Progress
    > = (
      message: dependency_2.rpc_common.Hash,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<Progress>,
      options?: grpc_1.CallOptions | grpc_1.requestCallback<Progress>,
      callback?: grpc_1.requestCallback<Progress>,
    ): grpc_1.ClientUnaryCall => {
      return super.GetDownloadProgress(message, metadata, options, callback);
    };
    GetContainerObjects: GrpcUnaryServiceInterface<
      dependency_2.rpc_common.Guid,
      dependency_1.fs.ObjectList
    > = (
      message: dependency_2.rpc_common.Guid,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_1.fs.ObjectList>,
      options?:
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_1.fs.ObjectList>,
      callback?: grpc_1.requestCallback<dependency_1.fs.ObjectList>,
    ): grpc_1.ClientUnaryCall => {
      return super.GetContainerObjects(message, metadata, options, callback);
    };
    GetContainerRootHash: GrpcUnaryServiceInterface<
      dependency_2.rpc_common.Guid,
      dependency_2.rpc_common.Hash
    > = (
      message: dependency_2.rpc_common.Guid,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Hash>,
      options?:
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Hash>,
      callback?: grpc_1.requestCallback<dependency_2.rpc_common.Hash>,
    ): grpc_1.ClientUnaryCall => {
      return super.GetContainerRootHash(message, metadata, options, callback);
    };
    ImportObjectFromDisk: GrpcUnaryServiceInterface<
      ObjectFromDiskOptions,
      dependency_2.rpc_common.Guid
    > = (
      message: ObjectFromDiskOptions,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Guid>,
      options?:
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Guid>,
      callback?: grpc_1.requestCallback<dependency_2.rpc_common.Guid>,
    ): grpc_1.ClientUnaryCall => {
      return super.ImportObjectFromDisk(message, metadata, options, callback);
    };
    PublishToTracker: GrpcUnaryServiceInterface<
      PublishingOptions,
      dependency_2.rpc_common.Empty
    > = (
      message: PublishingOptions,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
      options?:
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
      callback?: grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
    ): grpc_1.ClientUnaryCall => {
      return super.PublishToTracker(message, metadata, options, callback);
    };
    DownloadContainer: GrpcUnaryServiceInterface<
      DownloadContainerOptions,
      dependency_2.rpc_common.Empty
    > = (
      message: DownloadContainerOptions,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
      options?:
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
      callback?: grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
    ): grpc_1.ClientUnaryCall => {
      return super.DownloadContainer(message, metadata, options, callback);
    };
    CopyToClipboard: GrpcUnaryServiceInterface<
      String,
      dependency_2.rpc_common.Empty
    > = (
      message: String,
      metadata:
        | grpc_1.Metadata
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
      options?:
        | grpc_1.CallOptions
        | grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
      callback?: grpc_1.requestCallback<dependency_2.rpc_common.Empty>,
    ): grpc_1.ClientUnaryCall => {
      return super.CopyToClipboard(message, metadata, options, callback);
    };
  }
}
