// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: rpc/tracker.proto
// </auto-generated>
#pragma warning disable 0414, 1591, 8981, 0612
#region Designer generated code

using grpc = global::Grpc.Core;

namespace Tracker {
  public static partial class Tracker
  {
    static readonly string __ServiceName = "tracker.Tracker";

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static void __Helper_SerializeMessage(global::Google.Protobuf.IMessage message, grpc::SerializationContext context)
    {
      #if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
      if (message is global::Google.Protobuf.IBufferMessage)
      {
        context.SetPayloadLength(message.CalculateSize());
        global::Google.Protobuf.MessageExtensions.WriteTo(message, context.GetBufferWriter());
        context.Complete();
        return;
      }
      #endif
      context.Complete(global::Google.Protobuf.MessageExtensions.ToByteArray(message));
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static class __Helper_MessageCache<T>
    {
      public static readonly bool IsBufferMessage = global::System.Reflection.IntrospectionExtensions.GetTypeInfo(typeof(global::Google.Protobuf.IBufferMessage)).IsAssignableFrom(typeof(T));
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static T __Helper_DeserializeMessage<T>(grpc::DeserializationContext context, global::Google.Protobuf.MessageParser<T> parser) where T : global::Google.Protobuf.IMessage<T>
    {
      #if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
      if (__Helper_MessageCache<T>.IsBufferMessage)
      {
        return parser.ParseFrom(context.PayloadAsReadOnlySequence());
      }
      #endif
      return parser.ParseFrom(context.PayloadAsNewBuffer());
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Fs.ObjectWithHash> __Marshaller_fs_ObjectWithHash = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Fs.ObjectWithHash.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Tracker.Empty> __Marshaller_tracker_Empty = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Tracker.Empty.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Tracker.Hash> __Marshaller_tracker_Hash = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Tracker.Hash.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Tracker.MarkRequest> __Marshaller_tracker_MarkRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Tracker.MarkRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Tracker.PeerRequest> __Marshaller_tracker_PeerRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Tracker.PeerRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Tracker.PeerResponse> __Marshaller_tracker_PeerResponse = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Tracker.PeerResponse.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Tracker.ContainerGuid> __Marshaller_tracker_ContainerGuid = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Tracker.ContainerGuid.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Tracker.ContainerRootHash> __Marshaller_tracker_ContainerRootHash = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Tracker.ContainerRootHash.Parser));

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Fs.ObjectWithHash, global::Tracker.Empty> __Method_Publish = new grpc::Method<global::Fs.ObjectWithHash, global::Tracker.Empty>(
        grpc::MethodType.ClientStreaming,
        __ServiceName,
        "Publish",
        __Marshaller_fs_ObjectWithHash,
        __Marshaller_tracker_Empty);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Tracker.Hash, global::Fs.ObjectWithHash> __Method_GetObjectTree = new grpc::Method<global::Tracker.Hash, global::Fs.ObjectWithHash>(
        grpc::MethodType.ServerStreaming,
        __ServiceName,
        "GetObjectTree",
        __Marshaller_tracker_Hash,
        __Marshaller_fs_ObjectWithHash);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Tracker.MarkRequest, global::Tracker.Empty> __Method_MarkReachable = new grpc::Method<global::Tracker.MarkRequest, global::Tracker.Empty>(
        grpc::MethodType.ClientStreaming,
        __ServiceName,
        "MarkReachable",
        __Marshaller_tracker_MarkRequest,
        __Marshaller_tracker_Empty);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Tracker.MarkRequest, global::Tracker.Empty> __Method_MarkUnreachable = new grpc::Method<global::Tracker.MarkRequest, global::Tracker.Empty>(
        grpc::MethodType.ClientStreaming,
        __ServiceName,
        "MarkUnreachable",
        __Marshaller_tracker_MarkRequest,
        __Marshaller_tracker_Empty);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Tracker.PeerRequest, global::Tracker.PeerResponse> __Method_GetPeerList = new grpc::Method<global::Tracker.PeerRequest, global::Tracker.PeerResponse>(
        grpc::MethodType.ServerStreaming,
        __ServiceName,
        "GetPeerList",
        __Marshaller_tracker_PeerRequest,
        __Marshaller_tracker_PeerResponse);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Tracker.ContainerGuid, global::Tracker.Hash> __Method_GetContainerRootHash = new grpc::Method<global::Tracker.ContainerGuid, global::Tracker.Hash>(
        grpc::MethodType.Unary,
        __ServiceName,
        "GetContainerRootHash",
        __Marshaller_tracker_ContainerGuid,
        __Marshaller_tracker_Hash);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Tracker.ContainerRootHash, global::Tracker.Empty> __Method_SetContainerRootHash = new grpc::Method<global::Tracker.ContainerRootHash, global::Tracker.Empty>(
        grpc::MethodType.Unary,
        __ServiceName,
        "SetContainerRootHash",
        __Marshaller_tracker_ContainerRootHash,
        __Marshaller_tracker_Empty);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Tracker.Hash, global::Tracker.Empty> __Method_DeleteObjectHash = new grpc::Method<global::Tracker.Hash, global::Tracker.Empty>(
        grpc::MethodType.Unary,
        __ServiceName,
        "DeleteObjectHash",
        __Marshaller_tracker_Hash,
        __Marshaller_tracker_Empty);

    /// <summary>Service descriptor</summary>
    public static global::Google.Protobuf.Reflection.ServiceDescriptor Descriptor
    {
      get { return global::Tracker.TrackerReflection.Descriptor.Services[0]; }
    }

    /// <summary>Base class for server-side implementations of Tracker</summary>
    [grpc::BindServiceMethod(typeof(Tracker), "BindService")]
    public abstract partial class TrackerBase
    {
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task<global::Tracker.Empty> Publish(grpc::IAsyncStreamReader<global::Fs.ObjectWithHash> requestStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task GetObjectTree(global::Tracker.Hash request, grpc::IServerStreamWriter<global::Fs.ObjectWithHash> responseStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task<global::Tracker.Empty> MarkReachable(grpc::IAsyncStreamReader<global::Tracker.MarkRequest> requestStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task<global::Tracker.Empty> MarkUnreachable(grpc::IAsyncStreamReader<global::Tracker.MarkRequest> requestStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task GetPeerList(global::Tracker.PeerRequest request, grpc::IServerStreamWriter<global::Tracker.PeerResponse> responseStream, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task<global::Tracker.Hash> GetContainerRootHash(global::Tracker.ContainerGuid request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task<global::Tracker.Empty> SetContainerRootHash(global::Tracker.ContainerRootHash request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::System.Threading.Tasks.Task<global::Tracker.Empty> DeleteObjectHash(global::Tracker.Hash request, grpc::ServerCallContext context)
      {
        throw new grpc::RpcException(new grpc::Status(grpc::StatusCode.Unimplemented, ""));
      }

    }

    /// <summary>Client for Tracker</summary>
    public partial class TrackerClient : grpc::ClientBase<TrackerClient>
    {
      /// <summary>Creates a new client for Tracker</summary>
      /// <param name="channel">The channel to use to make remote calls.</param>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public TrackerClient(grpc::ChannelBase channel) : base(channel)
      {
      }
      /// <summary>Creates a new client for Tracker that uses a custom <c>CallInvoker</c>.</summary>
      /// <param name="callInvoker">The callInvoker to use to make remote calls.</param>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public TrackerClient(grpc::CallInvoker callInvoker) : base(callInvoker)
      {
      }
      /// <summary>Protected parameterless constructor to allow creation of test doubles.</summary>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      protected TrackerClient() : base()
      {
      }
      /// <summary>Protected constructor to allow creation of configured clients.</summary>
      /// <param name="configuration">The client configuration.</param>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      protected TrackerClient(ClientBaseConfiguration configuration) : base(configuration)
      {
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncClientStreamingCall<global::Fs.ObjectWithHash, global::Tracker.Empty> Publish(grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return Publish(new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncClientStreamingCall<global::Fs.ObjectWithHash, global::Tracker.Empty> Publish(grpc::CallOptions options)
      {
        return CallInvoker.AsyncClientStreamingCall(__Method_Publish, null, options);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncServerStreamingCall<global::Fs.ObjectWithHash> GetObjectTree(global::Tracker.Hash request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return GetObjectTree(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncServerStreamingCall<global::Fs.ObjectWithHash> GetObjectTree(global::Tracker.Hash request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncServerStreamingCall(__Method_GetObjectTree, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncClientStreamingCall<global::Tracker.MarkRequest, global::Tracker.Empty> MarkReachable(grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return MarkReachable(new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncClientStreamingCall<global::Tracker.MarkRequest, global::Tracker.Empty> MarkReachable(grpc::CallOptions options)
      {
        return CallInvoker.AsyncClientStreamingCall(__Method_MarkReachable, null, options);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncClientStreamingCall<global::Tracker.MarkRequest, global::Tracker.Empty> MarkUnreachable(grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return MarkUnreachable(new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncClientStreamingCall<global::Tracker.MarkRequest, global::Tracker.Empty> MarkUnreachable(grpc::CallOptions options)
      {
        return CallInvoker.AsyncClientStreamingCall(__Method_MarkUnreachable, null, options);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncServerStreamingCall<global::Tracker.PeerResponse> GetPeerList(global::Tracker.PeerRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return GetPeerList(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncServerStreamingCall<global::Tracker.PeerResponse> GetPeerList(global::Tracker.PeerRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncServerStreamingCall(__Method_GetPeerList, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Tracker.Hash GetContainerRootHash(global::Tracker.ContainerGuid request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return GetContainerRootHash(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Tracker.Hash GetContainerRootHash(global::Tracker.ContainerGuid request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_GetContainerRootHash, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Tracker.Hash> GetContainerRootHashAsync(global::Tracker.ContainerGuid request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return GetContainerRootHashAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Tracker.Hash> GetContainerRootHashAsync(global::Tracker.ContainerGuid request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_GetContainerRootHash, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Tracker.Empty SetContainerRootHash(global::Tracker.ContainerRootHash request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return SetContainerRootHash(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Tracker.Empty SetContainerRootHash(global::Tracker.ContainerRootHash request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_SetContainerRootHash, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Tracker.Empty> SetContainerRootHashAsync(global::Tracker.ContainerRootHash request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return SetContainerRootHashAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Tracker.Empty> SetContainerRootHashAsync(global::Tracker.ContainerRootHash request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_SetContainerRootHash, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Tracker.Empty DeleteObjectHash(global::Tracker.Hash request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DeleteObjectHash(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Tracker.Empty DeleteObjectHash(global::Tracker.Hash request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_DeleteObjectHash, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Tracker.Empty> DeleteObjectHashAsync(global::Tracker.Hash request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DeleteObjectHashAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Tracker.Empty> DeleteObjectHashAsync(global::Tracker.Hash request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_DeleteObjectHash, null, options, request);
      }
      /// <summary>Creates a new instance of client from given <c>ClientBaseConfiguration</c>.</summary>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      protected override TrackerClient NewInstance(ClientBaseConfiguration configuration)
      {
        return new TrackerClient(configuration);
      }
    }

    /// <summary>Creates service definition that can be registered with a server</summary>
    /// <param name="serviceImpl">An object implementing the server-side handling logic.</param>
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    public static grpc::ServerServiceDefinition BindService(TrackerBase serviceImpl)
    {
      return grpc::ServerServiceDefinition.CreateBuilder()
          .AddMethod(__Method_Publish, serviceImpl.Publish)
          .AddMethod(__Method_GetObjectTree, serviceImpl.GetObjectTree)
          .AddMethod(__Method_MarkReachable, serviceImpl.MarkReachable)
          .AddMethod(__Method_MarkUnreachable, serviceImpl.MarkUnreachable)
          .AddMethod(__Method_GetPeerList, serviceImpl.GetPeerList)
          .AddMethod(__Method_GetContainerRootHash, serviceImpl.GetContainerRootHash)
          .AddMethod(__Method_SetContainerRootHash, serviceImpl.SetContainerRootHash)
          .AddMethod(__Method_DeleteObjectHash, serviceImpl.DeleteObjectHash).Build();
    }

    /// <summary>Register service method with a service binder with or without implementation. Useful when customizing the service binding logic.
    /// Note: this method is part of an experimental API that can change or be removed without any prior notice.</summary>
    /// <param name="serviceBinder">Service methods will be bound by calling <c>AddMethod</c> on this object.</param>
    /// <param name="serviceImpl">An object implementing the server-side handling logic.</param>
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    public static void BindService(grpc::ServiceBinderBase serviceBinder, TrackerBase serviceImpl)
    {
      serviceBinder.AddMethod(__Method_Publish, serviceImpl == null ? null : new grpc::ClientStreamingServerMethod<global::Fs.ObjectWithHash, global::Tracker.Empty>(serviceImpl.Publish));
      serviceBinder.AddMethod(__Method_GetObjectTree, serviceImpl == null ? null : new grpc::ServerStreamingServerMethod<global::Tracker.Hash, global::Fs.ObjectWithHash>(serviceImpl.GetObjectTree));
      serviceBinder.AddMethod(__Method_MarkReachable, serviceImpl == null ? null : new grpc::ClientStreamingServerMethod<global::Tracker.MarkRequest, global::Tracker.Empty>(serviceImpl.MarkReachable));
      serviceBinder.AddMethod(__Method_MarkUnreachable, serviceImpl == null ? null : new grpc::ClientStreamingServerMethod<global::Tracker.MarkRequest, global::Tracker.Empty>(serviceImpl.MarkUnreachable));
      serviceBinder.AddMethod(__Method_GetPeerList, serviceImpl == null ? null : new grpc::ServerStreamingServerMethod<global::Tracker.PeerRequest, global::Tracker.PeerResponse>(serviceImpl.GetPeerList));
      serviceBinder.AddMethod(__Method_GetContainerRootHash, serviceImpl == null ? null : new grpc::UnaryServerMethod<global::Tracker.ContainerGuid, global::Tracker.Hash>(serviceImpl.GetContainerRootHash));
      serviceBinder.AddMethod(__Method_SetContainerRootHash, serviceImpl == null ? null : new grpc::UnaryServerMethod<global::Tracker.ContainerRootHash, global::Tracker.Empty>(serviceImpl.SetContainerRootHash));
      serviceBinder.AddMethod(__Method_DeleteObjectHash, serviceImpl == null ? null : new grpc::UnaryServerMethod<global::Tracker.Hash, global::Tracker.Empty>(serviceImpl.DeleteObjectHash));
    }

  }
}
#endregion
