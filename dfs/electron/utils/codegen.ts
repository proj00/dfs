// tools/generate-mocks.ts
import * as fs from "fs";
import { writeFileSync } from "fs";
import { resolve } from "path";
import * as protobuf from "protobufjs";
import { Code, code, imp, joinCode } from "ts-poet";

const descriptorPath = resolve(__dirname, "../src/types/descriptors.json");
const outputDir = resolve(__dirname, "code_out");

const descriptor = JSON.parse(fs.readFileSync(descriptorPath, "utf-8"));
const root = protobuf.Root.fromJSON(descriptor);
root.resolveAll();
for (const service of Object.values(root.nested ?? {})) {
  if (service instanceof protobuf.Service) {
    const mockCode = generateMock(service);
    const outputPath = resolve(outputDir, `${service.name}Mock.ts`);
    writeFileSync(outputPath, mockCode.toString());
    console.log(`✅ Wrote ${outputPath}`);
  }
}

function generateMock(service: protobuf.Service): Code {
  const serviceName = service.name;
  const methods = Object.values(service.methods);

  const lines = methods.map((method) => {
    const methodName = method.name;
    const reqType = imp(`{ ${method.requestType} } from '../types'`);
    const resType = imp(`{ ${method.responseType} } from '../types'`);

    return code`
        async ${methodName}(request: ${reqType}): Promise<${resType}> {
          console.log('${methodName} called with', request);
          return {
            // TODO: Fill in mocked fields
          } as ${resType};
        }
      `;
  });

  return code`
      export class ${serviceName}Mock {
        ${joinCode(lines, { on: "\n\n" })}
      }
    `;
}
